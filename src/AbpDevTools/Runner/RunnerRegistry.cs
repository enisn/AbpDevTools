using System.Collections.Concurrent;

namespace AbpDevTools.Runner;

internal sealed class RunnerRegistry : IDisposable
{
    private readonly ConcurrentDictionary<string, RunnerContext> _contexts = new(StringComparer.Ordinal);
    private readonly string _logsDirectory;
    private long _sequence;
    private long _lastActivityUtcTicks = DateTimeOffset.UtcNow.UtcTicks;
    private bool _disposed;

    public RunnerRegistry(string? logsDirectory = null)
    {
        _logsDirectory = logsDirectory ?? RunnerPaths.LogsDirectory;
    }

    public DateTimeOffset LastActivity => new(Interlocked.Read(ref _lastActivityUtcTicks), TimeSpan.Zero);
    public long LatestSequence => Interlocked.Read(ref _sequence);

    public bool HasActiveApplications => _contexts.Values.Any(x => x.HasActiveApplications);

    public RunnerResponse Start(RunnerStartContextRequest request)
    {
        Touch();
        if (string.IsNullOrWhiteSpace(request.ContextKey) ||
            string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            return RunnerResponse.Failure("The runner context is missing its identity or working directory.");
        }

        if (request.Applications.Length == 0)
        {
            return RunnerResponse.Failure("The launch plan does not contain any applications.");
        }

        if (request.Applications.Any(x => string.IsNullOrWhiteSpace(x.Id) ||
                                          string.IsNullOrWhiteSpace(x.FileName) ||
                                          string.IsNullOrWhiteSpace(x.WorkingDirectory)))
        {
            return RunnerResponse.Failure("One or more launch-plan applications are incomplete.");
        }

        var context = _contexts.GetOrAdd(
            request.ContextKey,
            _ => new RunnerContext(request, NextSequence, Touch, _logsDirectory));

        if (!string.Equals(
                Path.GetFullPath(context.WorkingDirectory),
                Path.GetFullPath(request.WorkingDirectory),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            return RunnerResponse.Failure("The context key is already associated with a different working directory.");
        }

        var result = context.Start(request);
        return new RunnerResponse
        {
            AffectedCount = result.StartedCount,
            Messages = result.Messages,
            Contexts = new[] { context.GetSnapshot(includeInactive: true) },
            LatestSequence = LatestSequence
        };
    }

    public RunnerResponse List(string? contextKey, bool includeInactive)
    {
        Touch();
        IEnumerable<RunnerContext> contexts = _contexts.Values;
        if (!string.IsNullOrWhiteSpace(contextKey))
        {
            contexts = contexts.Where(x => string.Equals(x.ContextKey, contextKey, StringComparison.Ordinal));
        }

        var snapshots = contexts
            .Select(x => x.GetSnapshot(includeInactive))
            .Where(x => includeInactive || x.Applications.Any(a => a.IsActive))
            .OrderBy(x => x.WorkingDirectory, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new RunnerResponse
        {
            Contexts = snapshots,
            LatestSequence = LatestSequence
        };
    }

    public async Task<RunnerResponse> StopAsync(
        string? contextKey,
        string[] projectFilters,
        CancellationToken cancellationToken)
    {
        Touch();
        if (string.IsNullOrWhiteSpace(contextKey) || !_contexts.TryGetValue(contextKey, out var context))
        {
            return RunnerResponse.Failure("No managed runner context was found for the requested directory and YAML configuration.");
        }

        var result = await context.StopAsync(projectFilters, cancellationToken);
        Touch();
        return new RunnerResponse
        {
            AffectedCount = result.StoppedCount,
            Contexts = new[] { context.GetSnapshot(includeInactive: true) },
            Messages = result.MatchedCount == 0
                ? new[] { "No matching active applications were found." }
                : result.StoppedCount == result.MatchedCount
                    ? new[] { $"Stopped {result.StoppedCount} application(s)." }
                    : new[] { $"Stopped {result.StoppedCount} of {result.MatchedCount} application(s); inspect 'abpdev ps' for processes that are still running." },
            LatestSequence = LatestSequence
        };
    }

    public async Task<RunnerResponse> RestartAsync(
        string? contextKey,
        string? applicationId,
        string[] projectFilters,
        CancellationToken cancellationToken)
    {
        Touch();
        if (string.IsNullOrWhiteSpace(contextKey) || !_contexts.TryGetValue(contextKey, out var context))
        {
            return RunnerResponse.Failure("No managed runner context was found.");
        }

        var count = await context.RestartAsync(applicationId, projectFilters, cancellationToken);
        Touch();
        return new RunnerResponse
        {
            AffectedCount = count,
            Contexts = new[] { context.GetSnapshot(includeInactive: true) },
            Messages = count == 0
                ? new[] { "No matching applications were found." }
                : new[] { $"Restarted {count} application(s)." },
            LatestSequence = LatestSequence
        };
    }

    public RunnerResponse GetLogs(string? contextKey, string? applicationId, long afterSequence, int limit)
    {
        Touch();
        if (string.IsNullOrWhiteSpace(contextKey) || !_contexts.TryGetValue(contextKey, out var context))
        {
            return RunnerResponse.Failure("No managed runner context was found.");
        }

        var boundedLimit = Math.Clamp(limit, 1, 5_000);
        var orderedLogs = context.GetLogs(applicationId, afterSequence)
            .OrderBy(x => x.Sequence)
            .ToArray();
        var logs = afterSequence <= 0
            ? orderedLogs.TakeLast(boundedLimit).ToArray()
            : orderedLogs.Take(boundedLimit).ToArray();

        return new RunnerResponse
        {
            Logs = logs,
            LatestSequence = logs.Length > 0 ? logs[^1].Sequence : LatestSequence
        };
    }

    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var context in _contexts.Values)
        {
            await context.StopAsync(Array.Empty<string>(), cancellationToken);
        }

        Touch();
    }

    private long NextSequence() => Interlocked.Increment(ref _sequence);

    private void Touch()
    {
        Interlocked.Exchange(ref _lastActivityUtcTicks, DateTimeOffset.UtcNow.UtcTicks);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var context in _contexts.Values)
        {
            context.Dispose();
        }

        _contexts.Clear();
    }
}

internal sealed class RunnerContext : IDisposable
{
    private readonly object _sync = new();
    private readonly Dictionary<string, ManagedRunnerApplication> _applications = new(StringComparer.Ordinal);
    private readonly Func<long> _nextSequence;
    private readonly Action _stateChanged;
    private bool _disposed;

    public RunnerContext(
        RunnerStartContextRequest request,
        Func<long> nextSequence,
        Action stateChanged,
        string logsDirectory)
    {
        ContextKey = request.ContextKey;
        DisplayName = request.DisplayName;
        WorkingDirectory = request.WorkingDirectory;
        ConfigurationPath = request.ConfigurationPath;
        ConfigurationHash = request.ConfigurationHash;
        CreatedAt = DateTimeOffset.UtcNow;
        _nextSequence = nextSequence;
        _stateChanged = stateChanged;
        LogsDirectory = logsDirectory;
    }

    public string ContextKey { get; }
    public string DisplayName { get; }
    public string WorkingDirectory { get; }
    public string? ConfigurationPath { get; }
    public string? ConfigurationHash { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    private string LogsDirectory { get; }

    public bool HasActiveApplications
    {
        get
        {
            lock (_sync)
            {
                return _applications.Values.Any(x => x.IsActive);
            }
        }
    }

    public RunnerContextStartResult Start(RunnerStartContextRequest request)
    {
        var messages = new List<string>();
        var toStart = new List<ManagedRunnerApplication>();
        var toReconcile = new List<(ManagedRunnerApplication Application, RunnerApplicationSpec Spec)>();

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!string.Equals(ConfigurationHash, request.ConfigurationHash, StringComparison.Ordinal) &&
                _applications.Values.Any(x => x.IsActive))
            {
                messages.Add("The YAML configuration changed since this context started; active applications keep their original launch plan.");
            }
            else
            {
                ConfigurationHash = request.ConfigurationHash;
            }

            foreach (var spec in request.Applications)
            {
                if (_applications.TryGetValue(spec.Id, out var existing))
                {
                    toReconcile.Add((existing, spec));
                    continue;
                }

                var application = new ManagedRunnerApplication(
                    ContextKey,
                    spec,
                    _nextSequence,
                    _stateChanged,
                    LogsDirectory);
                _applications.Add(spec.Id, application);
                toStart.Add(application);
            }
        }

        var startedCount = 0;
        foreach (var application in toStart)
        {
            application.Start();
            startedCount++;
            messages.Add($"Started {application.GetSnapshot().DisplayName}.");
        }

        foreach (var (application, spec) in toReconcile)
        {
            if (application.Reconcile(spec, out var message))
            {
                startedCount++;
            }

            messages.Add(message);
        }

        _stateChanged();
        return new RunnerContextStartResult(startedCount, messages.ToArray());
    }

    public async Task<RunnerContextStopResult> StopAsync(
        string[] projectFilters,
        CancellationToken cancellationToken)
    {
        ManagedRunnerApplication[] targets;
        lock (_sync)
        {
            targets = _applications.Values
                .Where(x => x.IsActive)
                .Where(x => projectFilters.Length == 0 ||
                            projectFilters.Any(filter => RunnerProjectMatcher.Matches(x.GetSnapshot(), filter)))
                .ToArray();
        }

        var results = await Task.WhenAll(targets.Select(x => x.StopAsync(cancellationToken)));
        _stateChanged();
        return new RunnerContextStopResult(targets.Length, results.Count(x => x));
    }

    public async Task<int> RestartAsync(
        string? applicationId,
        string[] projectFilters,
        CancellationToken cancellationToken)
    {
        ManagedRunnerApplication[] targets;
        lock (_sync)
        {
            targets = _applications.Values
                .Where(x => string.IsNullOrWhiteSpace(applicationId) ||
                            string.Equals(x.Id, applicationId, StringComparison.Ordinal))
                .Where(x => projectFilters.Length == 0 ||
                            projectFilters.Any(filter => RunnerProjectMatcher.Matches(x.GetSnapshot(), filter)))
                .ToArray();
        }

        var restartedCount = 0;
        foreach (var target in targets)
        {
            if (await target.RestartAsync(cancellationToken))
            {
                restartedCount++;
            }
        }

        _stateChanged();
        return restartedCount;
    }

    public RunnerContextSnapshot GetSnapshot(bool includeInactive)
    {
        RunnerApplicationSnapshot[] applications;
        lock (_sync)
        {
            applications = _applications.Values
                .Select(x => x.GetSnapshot())
                .Where(x => includeInactive || x.IsActive)
                .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return new RunnerContextSnapshot
        {
            ContextKey = ContextKey,
            DisplayName = DisplayName,
            WorkingDirectory = WorkingDirectory,
            ConfigurationPath = ConfigurationPath,
            ConfigurationHash = ConfigurationHash,
            CreatedAt = CreatedAt,
            Applications = applications
        };
    }

    public RunnerLogEntry[] GetLogs(string? applicationId, long afterSequence)
    {
        lock (_sync)
        {
            return _applications.Values
                .Where(x => string.IsNullOrWhiteSpace(applicationId) ||
                            string.Equals(x.Id, applicationId, StringComparison.Ordinal))
                .SelectMany(x => x.GetLogs(afterSequence))
                .ToArray();
        }
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
            foreach (var application in _applications.Values)
            {
                application.Dispose();
            }

            _applications.Clear();
        }
    }
}

internal sealed record RunnerContextStartResult(int StartedCount, string[] Messages);
internal sealed record RunnerContextStopResult(int MatchedCount, int StoppedCount);
