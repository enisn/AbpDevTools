using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AbpDevTools.Runner;

public static class RunnerProtocol
{
    public const int Version = 1;

    public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public enum RunnerRequestKind
{
    Ping,
    Start,
    List,
    Stop,
    Restart,
    Logs
}

public enum RunnerApplicationType
{
    DotNet,
    Npm,
    InstallLibs,
    Other
}

public enum RunnerApplicationState
{
    Starting,
    Running,
    Restarting,
    Stopping,
    Stopped,
    Exited,
    Failed
}

public enum RunnerReadinessState
{
    Unknown,
    Waiting,
    Ready,
    NotReady
}

public enum RunnerLogStream
{
    StandardOutput,
    StandardError,
    System
}

public sealed class RunnerRequest
{
    public int ProtocolVersion { get; set; } = RunnerProtocol.Version;
    public string Token { get; set; } = string.Empty;
    public RunnerRequestKind Kind { get; set; }
    public RunnerStartContextRequest? Start { get; set; }
    public string? ContextKey { get; set; }
    public string[] ProjectFilters { get; set; } = Array.Empty<string>();
    public string? ApplicationId { get; set; }
    public long AfterSequence { get; set; }
    public int Limit { get; set; } = 500;
    public bool IncludeInactive { get; set; }
}

public sealed class RunnerResponse
{
    public int ProtocolVersion { get; set; } = RunnerProtocol.Version;
    public bool Success { get; set; } = true;
    public string? Error { get; set; }
    public string[] Messages { get; set; } = Array.Empty<string>();
    public RunnerContextSnapshot[] Contexts { get; set; } = Array.Empty<RunnerContextSnapshot>();
    public RunnerLogEntry[] Logs { get; set; } = Array.Empty<RunnerLogEntry>();
    public int AffectedCount { get; set; }
    public long LatestSequence { get; set; }

    public static RunnerResponse Failure(string error) => new()
    {
        Success = false,
        Error = error
    };
}

public sealed class RunnerStartContextRequest
{
    public string ContextKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string? ConfigurationPath { get; set; }
    public string? ConfigurationHash { get; set; }
    public RunnerApplicationSpec[] Applications { get; set; } = Array.Empty<RunnerApplicationSpec>();
}

public sealed class RunnerApplicationSpec
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public RunnerApplicationType Type { get; set; }
    public string TargetPath { get; set; } = string.Empty;
    public string? Script { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public Dictionary<string, string?> Environment { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool RedirectStandardInput { get; set; }
    public bool Retry { get; set; }
    public bool Verbose { get; set; }

    [JsonIgnore]
    public string Fingerprint => RunnerContextIdentity.Hash(
        Type + "\n" +
        FileName + "\n" +
        Arguments + "\n" +
        WorkingDirectory + "\n" +
        RedirectStandardInput + "\n" +
        Retry + "\n" +
        Verbose + "\n" +
        string.Join("\n", Environment.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Select(x => $"{x.Key}={x.Value}")));

    public static RunnerApplicationSpec FromProcessStartInfo(
        string id,
        string name,
        string displayName,
        RunnerApplicationType type,
        string targetPath,
        string? script,
        ProcessStartInfo startInfo,
        bool retry,
        bool verbose)
    {
        return new RunnerApplicationSpec
        {
            Id = id,
            Name = name,
            DisplayName = displayName,
            Type = type,
            TargetPath = targetPath,
            Script = script,
            FileName = startInfo.FileName,
            Arguments = startInfo.Arguments,
            WorkingDirectory = startInfo.WorkingDirectory,
            Environment = startInfo.Environment.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
            RedirectStandardInput = startInfo.RedirectStandardInput,
            Retry = retry,
            Verbose = verbose
        };
    }

    public ProcessStartInfo CreateProcessStartInfo()
    {
        var startInfo = new ProcessStartInfo(FileName, Arguments)
        {
            WorkingDirectory = WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = RedirectStandardInput,
            CreateNoWindow = true
        };

        startInfo.Environment.Clear();
        foreach (var pair in Environment)
        {
            startInfo.Environment[pair.Key] = pair.Value;
        }

        return startInfo;
    }
}

public sealed class RunnerContextSnapshot
{
    public string ContextKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string? ConfigurationPath { get; set; }
    public string? ConfigurationHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public RunnerApplicationSnapshot[] Applications { get; set; } = Array.Empty<RunnerApplicationSnapshot>();

    [JsonIgnore]
    public bool IsActive => Applications.Any(x => x.IsActive);
}

public sealed class RunnerApplicationSnapshot
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public RunnerApplicationType Type { get; set; }
    public string TargetPath { get; set; } = string.Empty;
    public string? Script { get; set; }
    public string WorkingDirectory { get; set; } = string.Empty;
    public RunnerApplicationState State { get; set; }
    public RunnerReadinessState Readiness { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Url { get; set; }
    public int? ProcessId { get; set; }
    public int? ExitCode { get; set; }
    public int RestartCount { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public string LogFilePath { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsActive => State is RunnerApplicationState.Starting
        or RunnerApplicationState.Running
        or RunnerApplicationState.Restarting
        or RunnerApplicationState.Stopping;
}

public sealed class RunnerLogEntry
{
    public long Sequence { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string ContextKey { get; set; } = string.Empty;
    public string ApplicationId { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = string.Empty;
    public RunnerLogStream Stream { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed record RunnerContextDescriptor(
    string ContextKey,
    string DisplayName,
    string WorkingDirectory,
    string? ConfigurationPath,
    string? ConfigurationHash);

public static class RunnerContextIdentity
{
    public static RunnerContextDescriptor Create(string workingDirectory, string? configurationPath)
    {
        var canonicalWorkingDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workingDirectory));
        var canonicalConfigurationPath = string.IsNullOrWhiteSpace(configurationPath)
            ? null
            : Path.GetFullPath(configurationPath);
        var identity = NormalizeForIdentity(canonicalWorkingDirectory) + "\n" +
                       NormalizeForIdentity(canonicalConfigurationPath ?? string.Empty);
        var displayName = new DirectoryInfo(canonicalWorkingDirectory).Name;
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = canonicalWorkingDirectory;
        }

        return new RunnerContextDescriptor(
            Hash(identity),
            displayName,
            canonicalWorkingDirectory,
            canonicalConfigurationPath,
            ComputeConfigurationHash(canonicalConfigurationPath));
    }

    public static string CreateApplicationId(string targetPath, string? script = null, string? discriminator = null)
    {
        var canonicalTarget = Path.GetFullPath(targetPath);
        return Hash(
            NormalizeForIdentity(canonicalTarget) + "\n" +
            (script ?? string.Empty).ToUpperInvariant() + "\n" +
            (discriminator ?? string.Empty).ToUpperInvariant());
    }

    public static string Hash(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant()[..24];
    }

    private static string NormalizeForIdentity(string value)
    {
        var normalized = value.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return OperatingSystem.IsWindows() ? normalized.ToUpperInvariant() : normalized;
    }

    private static string? ComputeConfigurationHash(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return null;
        }

        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()[..16];
    }
}

public static class RunnerProjectMatcher
{
    public static bool Matches(RunnerApplicationSpec application, string filter)
    {
        return Contains(application.Id, filter) ||
               Contains(application.Name, filter) ||
               Contains(application.DisplayName, filter) ||
               Contains(application.TargetPath, filter) ||
               Contains(application.WorkingDirectory, filter) ||
               Contains(application.Script, filter);
    }

    public static bool Matches(RunnerApplicationSnapshot application, string filter)
    {
        return Contains(application.Id, filter) ||
               Contains(application.Name, filter) ||
               Contains(application.DisplayName, filter) ||
               Contains(application.TargetPath, filter) ||
               Contains(application.WorkingDirectory, filter) ||
               Contains(application.Script, filter);
    }

    private static bool Contains(string? value, string filter)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }
}
