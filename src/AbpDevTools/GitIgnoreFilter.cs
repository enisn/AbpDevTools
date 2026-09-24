using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace AbpDevTools;

// Filters discovered files through git, so anything ignored by .gitignore, .git/info/exclude or the global
// excludes file is skipped. Git also doesn't list files inside nested repositories or worktrees
// (e.g. '.claude/worktrees/*'), which would otherwise be discovered as duplicate projects.
internal static class GitIgnoreFilter
{
    // Returns filePaths unchanged when rootPath isn't inside a git work tree, git isn't installed,
    // or git lists no files for rootPath (for example when rootPath itself is ignored).
    public static IEnumerable<string> ExcludeIgnoredFiles(string rootPath, IEnumerable<string> filePaths)
    {
        var gitFiles = ListGitFiles(rootPath);

        if (gitFiles is null)
        {
            return filePaths;
        }

        return filePaths.Where(filePath => gitFiles.Contains(Path.GetFullPath(filePath)));
    }

    private static HashSet<string>? ListGitFiles(string rootPath)
    {
        // git can't list untracked files together with submodule files, so they are listed separately.
        var trackedFiles = RunLsFiles(rootPath, "--cached", "--recurse-submodules");
        if (trackedFiles is null)
        {
            return null;
        }

        var untrackedFiles = RunLsFiles(rootPath, "--others", "--exclude-standard");
        if (untrackedFiles is null || trackedFiles.Length + untrackedFiles.Length == 0)
        {
            return null;
        }

        var pathComparer = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        return trackedFiles
            .Concat(untrackedFiles)
            .Select(relativePath => Path.GetFullPath(Path.Combine(rootPath, relativePath)))
            .ToHashSet(pathComparer);
    }

    private static string[]? RunLsFiles(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("ls-files");
        startInfo.ArgumentList.Add("-z");

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var errorTask = process.StandardError.ReadToEndAsync();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            errorTask.Wait();

            return process.ExitCode == 0
                ? output.Split('\0', StringSplitOptions.RemoveEmptyEntries)
                : null;
        }
        catch (Win32Exception)
        {
            // git is not installed or not on PATH.
            return null;
        }
    }
}
