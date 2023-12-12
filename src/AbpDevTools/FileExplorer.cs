namespace AbpDevTools;

[RegisterTransient]
public class FileExplorer
{
    public IEnumerable<string> FindDescendants(string path, string pattern)
    {
        return Directory.EnumerateFiles(path, pattern, SearchOption.AllDirectories);
    }

    public IEnumerable<string> FindDescendants(string path, string pattern, string[] excludeFolders)
    {
        return Directory.EnumerateFiles(path, pattern, SearchOption.AllDirectories)
            .Where(x => !excludeFolders.Any(x.Contains));
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
