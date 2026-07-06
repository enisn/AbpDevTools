namespace AbpDevTools;

[RegisterTransient]
public class FileExplorer
{
    private static readonly string[] DefaultExcludedFolders = { ".git" };

    public IEnumerable<string> FindDescendants(string path, string pattern)
    {
        return FindDescendants(path, pattern, Array.Empty<string>());
    }

    public IEnumerable<string> FindDescendants(string path, string pattern, string[] excludeFolders)
    {
        var excludedFolders = DefaultExcludedFolders
            .Concat(excludeFolders ?? Array.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Directory.EnumerateFiles(path, pattern, SearchOption.AllDirectories)
            .Where(x => !IsInExcludedFolder(path, x, excludedFolders));
    }

    private static bool IsInExcludedFolder(string rootPath, string filePath, string[] excludedFolders)
    {
        var relativePath = Path.GetRelativePath(rootPath, filePath);
        var directoryPath = Path.GetDirectoryName(relativePath);

        if (string.IsNullOrEmpty(directoryPath))
        {
            return false;
        }

        var directoryNames = directoryPath.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        return directoryNames.Any(directoryName => excludedFolders.Contains(directoryName, StringComparer.OrdinalIgnoreCase));
    }

    public IEnumerable<string> FindAscendants(string path, string pattern)
    {
        foreach (var item in Directory.EnumerateFiles(path, pattern, SearchOption.TopDirectoryOnly))
        {
            yield return item;
        }

        var upLevelPath = Path.GetFullPath(Path.Combine(path, ".."));

        var root = Path.GetPathRoot(path);

        if (upLevelPath != root)
        {
            foreach (var item in FindAscendants(upLevelPath, pattern))
            {
                yield return item;
            }
        }
    }
}
