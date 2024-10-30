using AbpDevTools.Configuration;

namespace AbpDevTools.Services;

[RegisterSingleton]
public class RunnableProjectsProvider
{
    public RunnableProjectsProvider(RunConfiguration runConfiguration)
    {
        runConfiguration.CleanObsolete();
    }

    public FileInfo[] GetRunnableProjects(string path)
    {
        return Directory.EnumerateFiles(path, "*.csproj", SearchOption.AllDirectories)
            .Where(DoesHaveProgramClass)
            .Select(x => new FileInfo(x))
            .ToArray();
    }

    private static bool DoesHaveProgramClass(string projectPath)
    {
        return File.Exists(
            Path.Combine(
                Path.GetDirectoryName(projectPath)!,
                "Program.cs"
            ));
    }
}
