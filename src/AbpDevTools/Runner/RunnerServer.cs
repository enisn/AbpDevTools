using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AbpDevTools.Runner;

public sealed class RunnerServer
{
    private static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromSeconds(10);
    private readonly RunnerRegistry _registry;
    private readonly TimeSpan _idleTimeout;
    private readonly string _pipeName;
    private readonly string _token;
    private readonly string _instanceLockPath;
    private readonly string _logsDirectory;

    public RunnerServer(
        string? pipeName = null,
        string? token = null,
        TimeSpan? idleTimeout = null,
        string? instanceLockPath = null,
        string? logsDirectory = null)
    {
        _pipeName = pipeName ?? RunnerPaths.PipeName;
        _token = token ?? RunnerPaths.GetOrCreateToken();
        _idleTimeout = idleTimeout ?? DefaultIdleTimeout;
        _instanceLockPath = instanceLockPath ?? RunnerPaths.LockFilePath;
        _logsDirectory = logsDirectory ?? RunnerPaths.LogsDirectory;
        _registry = new RunnerRegistry(_logsDirectory);
    }

    public static Task<int> RunDefaultAsync(CancellationToken cancellationToken = default)
    {
        return new RunnerServer().RunAsync(cancellationToken);
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_instanceLockPath)!);
        Directory.CreateDirectory(_logsDirectory);

        FileStream instanceLock;
        try
        {
            instanceLock = new FileStream(
                _instanceLockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
        }
        catch (IOException)
        {
            // Another runner owns the per-user endpoint.
            return 0;
        }

        using (instanceLock)
        using (_registry)
        {
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var signalRegistrations = RunnerSignalRegistrations.Register(lifetime);

            ConsoleCancelEventHandler cancelHandler = (_, args) =>
            {
                // The runner can share a process group with the client that launched it.
                // Ctrl+C belongs to the client, which sends an explicit stop request.
                args.Cancel = true;
            };

            Console.CancelKeyPress += cancelHandler;
            try
            {
                while (!lifetime.IsCancellationRequested)
                {
                    if (!_registry.HasActiveApplications &&
                        DateTimeOffset.UtcNow - _registry.LastActivity >= _idleTimeout)
                    {
                        break;
                    }

                    using var pipe = CreateServerPipe();
                    using var acceptTimeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                    acceptTimeout.CancelAfter(TimeSpan.FromSeconds(1));

                    try
                    {
                        await pipe.WaitForConnectionAsync(acceptTimeout.Token);
                    }
                    catch (OperationCanceledException) when (!lifetime.IsCancellationRequested)
                    {
                        continue;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    if (pipe.IsConnected)
                    {
                        await HandleConnectionAsync(pipe, lifetime.Token);
                    }
                }
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
                using var shutdownTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await _registry.StopAllAsync(shutdownTimeout.Token);
                }
                catch
                {
                }
            }
        }

        return 0;
    }

    private NamedPipeServerStream CreateServerPipe()
    {
        return new NamedPipeServerStream(
            _pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true
        };

        RunnerResponse response;
        try
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line) || line.Length > 16 * 1024 * 1024)
            {
                response = RunnerResponse.Failure("The runner request was empty or too large.");
            }
            else
            {
                var request = JsonSerializer.Deserialize<RunnerRequest>(line, RunnerProtocol.JsonOptions);
                response = request is null
                    ? RunnerResponse.Failure("The runner request could not be parsed.")
                    : await HandleRequestAsync(request, cancellationToken);
            }
        }
        catch (JsonException ex)
        {
            response = RunnerResponse.Failure($"The runner request is invalid: {ex.Message}");
        }
        catch (Exception ex)
        {
            response = RunnerResponse.Failure($"Runner request failed: {ex.Message}");
        }

        try
        {
            var json = JsonSerializer.Serialize(response, RunnerProtocol.JsonOptions);
            await writer.WriteLineAsync(json.AsMemory(), cancellationToken);
        }
        catch (IOException)
        {
            // The client can disconnect while a long-running stop request completes.
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task<RunnerResponse> HandleRequestAsync(RunnerRequest request, CancellationToken cancellationToken)
    {
        if (request.ProtocolVersion != RunnerProtocol.Version)
        {
            return RunnerResponse.Failure(
                $"Runner protocol mismatch. Client={request.ProtocolVersion}, runner={RunnerProtocol.Version}.");
        }

        if (!TokenMatches(request.Token))
        {
            return RunnerResponse.Failure("Runner authentication failed.");
        }

        return request.Kind switch
        {
            RunnerRequestKind.Ping => new RunnerResponse(),
            RunnerRequestKind.Start when request.Start is not null => _registry.Start(request.Start),
            RunnerRequestKind.Start => RunnerResponse.Failure("The start request does not contain a launch plan."),
            RunnerRequestKind.List => _registry.List(request.ContextKey, request.IncludeInactive),
            RunnerRequestKind.Stop => await _registry.StopAsync(
                request.ContextKey,
                request.ProjectFilters,
                cancellationToken),
            RunnerRequestKind.Restart => await _registry.RestartAsync(
                request.ContextKey,
                request.ApplicationId,
                request.ProjectFilters,
                cancellationToken),
            RunnerRequestKind.Logs => _registry.GetLogs(
                request.ContextKey,
                request.ApplicationId,
                request.AfterSequence,
                request.Limit),
            _ => RunnerResponse.Failure($"Unsupported runner request '{request.Kind}'.")
        };
    }

    private bool TokenMatches(string candidate)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(_token);
        var candidateBytes = Encoding.UTF8.GetBytes(candidate ?? string.Empty);
        return expectedBytes.Length == candidateBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, candidateBytes);
    }
}

internal sealed class RunnerSignalRegistrations : IDisposable
{
    private readonly List<PosixSignalRegistration> _registrations = new();

    private RunnerSignalRegistrations()
    {
    }

    public static RunnerSignalRegistrations Register(CancellationTokenSource lifetime)
    {
        var registrations = new RunnerSignalRegistrations();
        if (OperatingSystem.IsWindows())
        {
            return registrations;
        }

        registrations._registrations.Add(PosixSignalRegistration.Create(PosixSignal.SIGHUP, context =>
        {
            // Closing the terminal that launched a detached runner must not stop applications.
            context.Cancel = true;
        }));
        registrations._registrations.Add(PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
        {
            context.Cancel = true;
            lifetime.Cancel();
        }));

        return registrations;
    }

    public void Dispose()
    {
        foreach (var registration in _registrations)
        {
            registration.Dispose();
        }

        _registrations.Clear();
    }
}
