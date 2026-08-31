using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AbpDevTools.Runner;

internal sealed class ManagedRunnerApplication : IDisposable
{
    private const int LogLimit = 2_000;
    private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(3);
    private static readonly Regex UrlRegex = new(@"https?://[^\s]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly string[] NpmReadyMarkers =
    {
        "ready in",
        "compiled successfully",
        "app running at",
        "local:",
        "network:"
    };

    private readonly object _sync = new();
    private readonly string _contextKey;
    private readonly Func<long> _nextSequence;
    private readonly Action _stateChanged;
    private readonly ConcurrentQueue<RunnerLogEntry> _logs = new();
    private readonly RunnerLogWriter _logWriter;
    private RunnerApplicationSpec _spec;
    private Process? _process;
    private CancellationTokenSource? _retryCancellation;
    private int _generation;
    private bool _stopRequested;
    private bool _disposed;
    private RunnerApplicationState _state = RunnerApplicationState.Stopped;
    private RunnerReadinessState _readiness = RunnerReadinessState.Unknown;
    private string _status = "Stopped";
    private string? _url;
    private int? _exitCode;
    private int _restartCount;
    private DateTimeOffset? _startedAt;

    public ManagedRunnerApplication(
        string contextKey,
        RunnerApplicationSpec spec,
        Func<long> nextSequence,
        Action stateChanged,
        string logsDirectory)
    {
        _contextKey = contextKey;
        _spec = spec;
        _nextSequence = nextSequence;
        _stateChanged = stateChanged;
        _logWriter = new RunnerLogWriter(logsDirectory, contextKey, spec.Id);
    }

    public string Id => _spec.Id;

    public string Fingerprint
    {
        get
        {
            lock (_sync)
            {
                return _spec.Fingerprint;
            }
        }
    }

    public bool IsActive
    {
        get
        {
            lock (_sync)
            {
                return IsActiveState(_state);
            }
        }
    }

    public void Start()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (IsActiveState(_state))
            {
                return;
            }

            _stopRequested = false;
            CancelRetryLocked();
        }

        StartProcess(isRestart: false);
    }

    public bool Reconcile(RunnerApplicationSpec spec, out string message)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (IsActiveState(_state))
            {
                message = string.Equals(_spec.Fingerprint, spec.Fingerprint, StringComparison.Ordinal)
                    ? $"{_spec.DisplayName} is already running."
                    : $"{_spec.DisplayName} is already running with a different launch plan; stop it and run again to apply changes.";
                return false;
            }

            _spec = spec;
            _stopRequested = false;
            CancelRetryLocked();
        }

        StartProcess(isRestart: false);
        message = $"Restarted {_spec.DisplayName}.";
        return true;
    }

    public async Task<bool> StopAsync(CancellationToken cancellationToken = default)
    {
        Process? process;
        lock (_sync)
        {
            if (_disposed)
            {
                return true;
            }

            _stopRequested = true;
            CancelRetryLocked();
            process = _process;

            if (!IsProcessRunning(process))
            {
                _state = RunnerApplicationState.Stopped;
                _readiness = RunnerReadinessState.NotReady;
                _status = "Stopped";
                _stateChanged();
                return true;
            }

            _state = RunnerApplicationState.Stopping;
            _status = "Stopping";
        }

        _stateChanged();
        AddLog(RunnerLogStream.System, "Stopping application.");

        try
        {
            process!.Kill(entireProcessTree: true);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (InvalidOperationException)
        {
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            AddLog(RunnerLogStream.System, "Timed out while waiting for the application to stop.");
        }
        catch (Exception ex)
        {
            AddLog(RunnerLogStream.System, $"Failed to stop application: {ex.Message}");
        }

        bool stopped;
        lock (_sync)
        {
            stopped = !IsProcessRunning(process);
            if (stopped)
            {
                _state = RunnerApplicationState.Stopped;
                _readiness = RunnerReadinessState.NotReady;
                _status = "Stopped";
                _process = null;
            }
            else
            {
                _state = RunnerApplicationState.Running;
                _status = "Failed to stop";
                _process = process;
            }
        }

        if (!stopped)
        {
            AddLog(RunnerLogStream.System, "The process is still running after the stop attempt.");
        }

        _stateChanged();
        return stopped;
    }

    public async Task<bool> RestartAsync(CancellationToken cancellationToken = default)
    {
        if (!await StopAsync(cancellationToken))
        {
            return false;
        }

        lock (_sync)
        {
            if (_disposed)
            {
                return false;
            }

            _stopRequested = false;
            _restartCount++;
            _state = RunnerApplicationState.Restarting;
            _readiness = RunnerReadinessState.Waiting;
            _status = "Restarting";
        }

        _stateChanged();
        StartProcess(isRestart: true);
        return IsActive;
    }

    public RunnerApplicationSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            int? processId = null;
            if (IsProcessRunning(_process))
            {
                try
                {
                    processId = _process!.Id;
                }
                catch (InvalidOperationException)
                {
                }
            }

            return new RunnerApplicationSnapshot
            {
                Id = _spec.Id,
                Name = _spec.Name,
                DisplayName = _spec.DisplayName,
                Type = _spec.Type,
                TargetPath = _spec.TargetPath,
                Script = _spec.Script,
                WorkingDirectory = _spec.WorkingDirectory,
                State = _state,
                Readiness = _readiness,
                Status = _status,
                Url = _url,
                ProcessId = processId,
                ExitCode = _exitCode,
                RestartCount = _restartCount,
                StartedAt = _startedAt,
                LogFilePath = _logWriter.FilePath
            };
        }
    }

    public RunnerLogEntry[] GetLogs(long afterSequence)
    {
        return _logs.Where(x => x.Sequence > afterSequence).ToArray();
    }

    private void StartProcess(bool isRestart)
    {
        RunnerApplicationSpec spec;
        int generation;
        lock (_sync)
        {
            ThrowIfDisposed();
            spec = _spec;
            generation = ++_generation;
            _state = isRestart ? RunnerApplicationState.Restarting : RunnerApplicationState.Starting;
            _readiness = spec.Type == RunnerApplicationType.InstallLibs
                ? RunnerReadinessState.Unknown
                : RunnerReadinessState.Waiting;
            _status = spec.Type == RunnerApplicationType.InstallLibs ? "Installing" : "Starting";
            _url = null;
            _exitCode = null;
        }

        _stateChanged();

        Process? process = null;
        try
        {
            process = new Process
            {
                StartInfo = spec.CreateProcessStartInfo(),
                EnableRaisingEvents = true
            };
            process.OutputDataReceived += (_, args) => OnOutput(process, generation, RunnerLogStream.StandardOutput, args.Data);
            process.ErrorDataReceived += (_, args) => OnOutput(process, generation, RunnerLogStream.StandardError, args.Data);
            process.Exited += (_, _) => OnExited(process, generation);

            var shouldAbort = false;
            lock (_sync)
            {
                if (_disposed || _stopRequested || generation != _generation)
                {
                    shouldAbort = true;
                }
                else
                {
                    _process = process;
                }
            }

            if (shouldAbort)
            {
                process.Dispose();
                return;
            }

            if (!process.Start())
            {
                throw new InvalidOperationException($"Process.Start returned false for '{spec.DisplayName}'.");
            }

            lock (_sync)
            {
                if (_disposed || _stopRequested || generation != _generation)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                    }

                    return;
                }

                _state = RunnerApplicationState.Running;
                _startedAt = DateTimeOffset.UtcNow;
            }

            AddLog(RunnerLogStream.System, $"Started process {process.Id}: {spec.FileName} {spec.Arguments}");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (spec.RedirectStandardInput)
            {
                process.StandardInput.Close();
            }

            _stateChanged();
        }
        catch (Exception ex)
        {
            process?.Dispose();
            lock (_sync)
            {
                if (generation == _generation)
                {
                    _process = null;
                    _state = RunnerApplicationState.Failed;
                    _readiness = RunnerReadinessState.NotReady;
                    _status = "Failed to start";
                }
            }

            AddLog(RunnerLogStream.System, $"Failed to start application: {ex.Message}");
            _stateChanged();
        }
    }

    private void OnOutput(Process process, int generation, RunnerLogStream stream, string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        lock (_sync)
        {
            if (_disposed || generation != _generation || !ReferenceEquals(process, _process))
            {
                return;
            }

            if (_spec.Verbose)
            {
                _status = Truncate(message, 240);
            }

            DetectReadinessLocked(message);
        }

        AddLog(stream, message);
        _stateChanged();
    }

    private void OnExited(Process process, int generation)
    {
        int exitCode;
        try
        {
            exitCode = process.ExitCode;
        }
        catch
        {
            exitCode = -1;
        }

        CancellationToken retryToken = default;
        var retry = false;

        lock (_sync)
        {
            if (_disposed || generation != _generation || !ReferenceEquals(process, _process))
            {
                return;
            }

            _exitCode = exitCode;
            _process = null;
            _readiness = RunnerReadinessState.NotReady;

            if (_stopRequested)
            {
                _state = RunnerApplicationState.Stopped;
                _status = "Stopped";
            }
            else if (_spec.Retry)
            {
                _state = RunnerApplicationState.Restarting;
                _status = $"Exited ({exitCode}); retrying";
                _retryCancellation = new CancellationTokenSource();
                retryToken = _retryCancellation.Token;
                retry = true;
            }
            else if (_spec.Type == RunnerApplicationType.InstallLibs && exitCode == 0)
            {
                _state = RunnerApplicationState.Exited;
                _readiness = RunnerReadinessState.Ready;
                _status = "Completed";
            }
            else
            {
                _state = RunnerApplicationState.Exited;
                _status = $"Exited ({exitCode})";
            }
        }

        AddLog(RunnerLogStream.System, $"Process exited with code {exitCode}.");
        _stateChanged();
        process.Dispose();

        if (retry)
        {
            _ = RetryAfterDelayAsync(generation, retryToken);
        }
    }

    private async Task RetryAfterDelayAsync(int generation, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(RestartDelay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (_sync)
        {
            if (_disposed || _stopRequested || generation != _generation || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _restartCount++;
            _retryCancellation?.Dispose();
            _retryCancellation = null;
        }

        StartProcess(isRestart: true);
    }

    private void DetectReadinessLocked(string message)
    {
        if (_spec.Type == RunnerApplicationType.DotNet)
        {
            if (message.Contains("Now listening on: ", StringComparison.Ordinal))
            {
                var markerIndex = message.IndexOf("Now listening on: ", StringComparison.Ordinal);
                _status = message[markerIndex..];
                _url = UrlRegex.Match(message).Value;
                _readiness = RunnerReadinessState.Ready;
            }
            else if (message.Contains("dotnet watch ", StringComparison.OrdinalIgnoreCase) &&
                     message.Contains(" Started", StringComparison.OrdinalIgnoreCase))
            {
                _status = Truncate(message, 240);
                _readiness = RunnerReadinessState.Ready;
            }
        }
        else if (_spec.Type == RunnerApplicationType.Npm)
        {
            var url = UrlRegex.Match(message);
            if (url.Success)
            {
                _url = url.Value;
                _status = url.Value;
                _readiness = RunnerReadinessState.Ready;
            }
            else if (NpmReadyMarkers.Any(marker => message.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            {
                _status = Truncate(message, 240);
                _readiness = RunnerReadinessState.Ready;
            }
        }
        else if (_spec.Type == RunnerApplicationType.InstallLibs &&
                 message.Contains("Done in", StringComparison.OrdinalIgnoreCase))
        {
            _status = "Completed";
            _readiness = RunnerReadinessState.Ready;
        }
    }

    private void AddLog(RunnerLogStream stream, string message)
    {
        var entry = new RunnerLogEntry
        {
            Sequence = _nextSequence(),
            Timestamp = DateTimeOffset.UtcNow,
            ContextKey = _contextKey,
            ApplicationId = _spec.Id,
            ApplicationName = _spec.DisplayName,
            Stream = stream,
            Message = message
        };

        _logs.Enqueue(entry);
        while (_logs.Count > LogLimit)
        {
            _logs.TryDequeue(out _);
        }

        try
        {
            _logWriter.Write(entry);
        }
        catch
        {
            // Runtime logging must not terminate a managed application.
        }
    }

    private void CancelRetryLocked()
    {
        _retryCancellation?.Cancel();
        _retryCancellation?.Dispose();
        _retryCancellation = null;
    }

    private static bool IsActiveState(RunnerApplicationState state)
    {
        return state is RunnerApplicationState.Starting
            or RunnerApplicationState.Running
            or RunnerApplicationState.Restarting
            or RunnerApplicationState.Stopping;
    }

    private static bool IsProcessRunning(Process? process)
    {
        if (process is null)
        {
            return false;
        }

        try
        {
            return !process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stopRequested = true;
            CancelRetryLocked();

            try
            {
                if (IsProcessRunning(_process))
                {
                    _process!.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }

            _process?.Dispose();
            _process = null;
        }

        _logWriter.Dispose();
    }
}
