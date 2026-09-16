using System.Diagnostics;

namespace AbpDevTools.Tests.Helpers;

/// <summary>
/// Runs the real git CLI for tests that need an actual repository, worktree or submodule.
/// </summary>
public static class GitTestHelper
{
    // Keeps fixture setup independent from the machine's git identity, commit signing, global excludes file
    // and file protocol settings. Discovery itself still honors global excludes; only the fixtures ignore them.
    private static readonly string[] IsolatedConfiguration =
    {
        "-c", "user.name=Foo",
        "-c", "user.email=foo@example.com",
        "-c", "commit.gpgsign=false",
        "-c", "core.excludesFile=/dev/null",
        "-c", "protocol.file.allow=always"
    };

    public static void InitRepository(string path)
    {
        Run(path, "init", "-q");
    }

    public static void CommitAll(string repositoryPath)
    {
        Run(repositoryPath, "add", "-A");
        Run(repositoryPath, "commit", "--no-verify", "-q", "-m", "test");
    }

    public static void AddExcludePattern(string repositoryPath, string pattern)
    {
        var infoDirectory = Path.Combine(repositoryPath, ".git", "info");
        Directory.CreateDirectory(infoDirectory);
        File.AppendAllText(Path.Combine(infoDirectory, "exclude"), pattern + Environment.NewLine);
    }

    public static void Run(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in IsolatedConfiguration.Concat(arguments))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var errorTask = process.StandardError.ReadToEndAsync();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        var error = errorTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"'git {string.Join(" ", arguments)}' failed: {error}");
        }
    }
}
