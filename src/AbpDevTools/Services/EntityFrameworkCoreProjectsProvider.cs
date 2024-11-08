namespace AbpDevTools.Services;

[RegisterSingleton]
public class EntityFrameworkCoreProjectsProvider
{
    public FileInfo[] GetEfCoreProjects(string path)
    {
        return Directory.EnumerateFiles(path, "*.csproj", SearchOption.AllDirectories)
            .Where(DoesHaveEfCoreReference)
            .Select(x => new FileInfo(x))
            .ToArray();
    }

    private static bool DoesHaveEfCoreReference(string projectPath)
    {
        using var fs = new FileStream(projectPath, FileMode.Open, FileAccess.Read);
        using var sr = new StreamReader(fs);

        while (!sr.EndOfStream)
        {
            var line = sr.ReadLine();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            if (line.Contains("Microsoft.EntityFrameworkCore"))
            {
                return true;
            }
        }

        return false;
    }
}