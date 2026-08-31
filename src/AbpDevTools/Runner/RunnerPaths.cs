using System.Security.Cryptography;
using System.Runtime.Versioning;
using System.Text;

namespace AbpDevTools.Runner;

public static class RunnerPaths
{
    private static readonly object TokenLock = new();
    private static string? _token;

    public static string BaseDirectory
    {
        get
        {
            var overrideDirectory = Environment.GetEnvironmentVariable("ABPDEV_RUNNER_HOME");
            if (!string.IsNullOrWhiteSpace(overrideDirectory))
            {
                return Path.GetFullPath(overrideDirectory);
            }

            var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localData))
            {
                localData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            }

            return Path.Combine(localData, "abpdev", "runner");
        }
    }

    public static string LogsDirectory => Path.Combine(BaseDirectory, "logs");
    public static string LockFilePath => Path.Combine(BaseDirectory, "runner.lock");
    public static string TokenFilePath => Path.Combine(BaseDirectory, "runner.token");

    public static string PipeName
    {
        get
        {
            var identity = Environment.UserName + "\n" +
                           Environment.UserDomainName + "\n" +
                           Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\n" +
                           BaseDirectory;
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
            return "abpdev-runner-" + Convert.ToHexString(hash).ToLowerInvariant()[..20];
        }
    }

    public static string GetOrCreateToken()
    {
        lock (TokenLock)
        {
            if (!string.IsNullOrEmpty(_token))
            {
                return _token;
            }

            EnsureDirectories();

            var generatedToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    using var stream = new FileStream(
                        TokenFilePath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None);
                    using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                    var token = reader.ReadToEnd().Trim();
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        stream.SetLength(0);
                        stream.Position = 0;
                        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
                        writer.Write(generatedToken);
                        writer.Flush();
                        token = generatedToken;
                    }

                    _token = token;
                    break;
                }
                catch (IOException) when (attempt < 20)
                {
                    Thread.Sleep(25);
                }
            }

            SecureTokenFile();
            return _token;
        }
    }

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(BaseDirectory);
        Directory.CreateDirectory(LogsDirectory);

        if (!OperatingSystem.IsWindows())
        {
            TrySetUnixMode(BaseDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            TrySetUnixMode(LogsDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static void SecureTokenFile()
    {
        if (!OperatingSystem.IsWindows() && File.Exists(TokenFilePath))
        {
            TrySetUnixMode(TokenFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [UnsupportedOSPlatform("windows")]
    private static void TrySetUnixMode(string path, UnixFileMode mode)
    {
        try
        {
            File.SetUnixFileMode(path, mode);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (PlatformNotSupportedException)
        {
        }
    }
}
