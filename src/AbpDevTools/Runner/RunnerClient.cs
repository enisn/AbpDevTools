using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace AbpDevTools.Runner;

public interface IRunnerClient
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    Task<RunnerResponse> StartAsync(RunnerStartContextRequest request, CancellationToken cancellationToken = default);
    Task<RunnerResponse?> ListAsync(string? contextKey = null, bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<RunnerResponse?> StopAsync(string contextKey, string[] projectFilters, CancellationToken cancellationToken = default);
    Task<RunnerResponse?> RestartAsync(string contextKey, string? applicationId = null, string[]? projectFilters = null, CancellationToken cancellationToken = default);
    Task<RunnerResponse?> GetLogsAsync(string contextKey, string? applicationId, long afterSequence, int limit, CancellationToken cancellationToken = default);
}

public sealed class RunnerClient : IRunnerClient
{
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    private readonly string _pipeName;
    private readonly bool _canStartServer;
    private readonly object _tokenLock = new();
    private string? _token;

    public RunnerClient()
    {
        _pipeName = RunnerPaths.PipeName;
        _canStartServer = true;
    }

    internal RunnerClient(string pipeName, string token, bool canStartServer = false)
    {
        _pipeName = pipeName;
        _token = token;
        _canStartServer = canStartServer;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var response = await TrySendAsync(
            new RunnerRequest { Kind = RunnerRequestKind.Ping },
            cancellationToken);
        return response?.Success == true;
    }

    public async Task<RunnerResponse> StartAsync(
        RunnerStartContextRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureServerAsync(cancellationToken);
        var response = await SendAsync(
            new RunnerRequest
            {
                Kind = RunnerRequestKind.Start,
                Start = request,
                ContextKey = request.ContextKey,
                IncludeInactive = true
            },
            cancellationToken);

        return response;
    }

    public Task<RunnerResponse?> ListAsync(
        string? contextKey = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        return TrySendAsync(
            new RunnerRequest
            {
                Kind = RunnerRequestKind.List,
                ContextKey = contextKey,
                IncludeInactive = includeInactive
            },
            cancellationToken);
    }

    public Task<RunnerResponse?> StopAsync(
        string contextKey,
        string[] projectFilters,
        CancellationToken cancellationToken = default)
    {
        return TrySendAsync(
            new RunnerRequest
            {
                Kind = RunnerRequestKind.Stop,
                ContextKey = contextKey,
                ProjectFilters = projectFilters
            },
            cancellationToken);
    }

    public Task<RunnerResponse?> RestartAsync(
        string contextKey,
        string? applicationId = null,
        string[]? projectFilters = null,
        CancellationToken cancellationToken = default)
    {
        return TrySendAsync(
            new RunnerRequest
            {
                Kind = RunnerRequestKind.Restart,
                ContextKey = contextKey,
                ApplicationId = applicationId,
                ProjectFilters = projectFilters ?? Array.Empty<string>()
            },
            cancellationToken);
    }

    public Task<RunnerResponse?> GetLogsAsync(
        string contextKey,
        string? applicationId,
        long afterSequence,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return TrySendAsync(
            new RunnerRequest
            {
                Kind = RunnerRequestKind.Logs,
                ContextKey = contextKey,
                ApplicationId = applicationId,
                AfterSequence = afterSequence,
                Limit = limit
            },
            cancellationToken);
    }

    private async Task EnsureServerAsync(CancellationToken cancellationToken)
    {
        if (await IsAvailableAsync(cancellationToken))
        {
            return;
        }

        if (!_canStartServer)
        {
            throw new RunnerClientException("The centralized runner is not available.");
        }

        StartServerProcess();

        for (var attempt = 0; attempt < 50; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(100, cancellationToken);
            if (await IsAvailableAsync(cancellationToken))
            {
                return;
            }
        }

        throw new RunnerClientException("The centralized runner did not become available within five seconds.");
    }

    private void StartServerProcess()
    {
        var startInfo = CreateServerProcessStartInfo();
        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                throw new RunnerClientException("Process.Start returned null while starting the centralized runner.");
            }
        }
        catch (RunnerClientException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new RunnerClientException($"Failed to start the centralized runner: {ex.Message}", ex);
        }
    }

    internal static ProcessStartInfo CreateServerProcessStartInfo()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new RunnerClientException("The current executable path could not be determined.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = processPath,
            WorkingDirectory = RunnerPaths.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true
        };

        var entryAssemblyPath = Assembly.GetEntryAssembly()?.Location;
        var processName = Path.GetFileNameWithoutExtension(processPath);
        if (string.Equals(processName, "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(entryAssemblyPath))
            {
                throw new RunnerClientException("The AbpDevTools entry assembly path could not be determined.");
            }

            startInfo.ArgumentList.Add(entryAssemblyPath);
        }

        startInfo.ArgumentList.Add(Program.RunnerServerArgument);
        return startInfo;
    }

    private async Task<RunnerResponse?> TrySendAsync(
        RunnerRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await SendAsync(request, cancellationToken);
        }
        catch (TimeoutException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private async Task<RunnerResponse> SendAsync(
        RunnerRequest request,
        CancellationToken cancellationToken)
    {
        request.ProtocolVersion = RunnerProtocol.Version;
        request.Token = GetToken();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);
        using var pipe = new NamedPipeClientStream(
            ".",
            _pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        await pipe.ConnectAsync((int)ConnectionTimeout.TotalMilliseconds, timeout.Token);

        using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true
        };

        var json = JsonSerializer.Serialize(request, RunnerProtocol.JsonOptions);
        await writer.WriteLineAsync(json.AsMemory(), timeout.Token);
        var responseLine = await reader.ReadLineAsync(timeout.Token);
        if (string.IsNullOrWhiteSpace(responseLine))
        {
            throw new IOException("The centralized runner closed the connection without a response.");
        }

        var response = JsonSerializer.Deserialize<RunnerResponse>(responseLine, RunnerProtocol.JsonOptions)
            ?? throw new IOException("The centralized runner returned an invalid response.");

        if (response.ProtocolVersion != RunnerProtocol.Version)
        {
            throw new RunnerClientException(
                $"Runner protocol mismatch. Client={RunnerProtocol.Version}, runner={response.ProtocolVersion}.");
        }

        return response;
    }

    private string GetToken()
    {
        lock (_tokenLock)
        {
            return _token ??= RunnerPaths.GetOrCreateToken();
        }
    }
}

public sealed class RunnerClientException : Exception
{
    public RunnerClientException(string message)
        : base(message)
    {
    }

    public RunnerClientException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
