namespace AbpDevTools.Services;

[RegisterSingleton]
public class EntityFrameworkCoreProjectsProvider
{
    private readonly DotnetDependencyResolver _dependencyResolver;

    public EntityFrameworkCoreProjectsProvider(DotnetDependencyResolver dependencyResolver)
    {
        _dependencyResolver = dependencyResolver;
    }

    public async Task<FileInfo[]> GetEfCoreProjectsAsync(string path, CancellationToken cancellationToken = default)
    {
        var projects = Directory.EnumerateFiles(path, "*.csproj", SearchOption.AllDirectories);
        var efCoreProjects = new List<FileInfo>();

        foreach (var projectPath in projects)
        {
            if (await DoesHaveEfCoreReferenceAsync(projectPath, cancellationToken))
            {
                efCoreProjects.Add(new FileInfo(projectPath));
            }
        }

        return efCoreProjects.ToArray();
    }

    private async Task<bool> DoesHaveEfCoreReferenceAsync(string projectPath, CancellationToken cancellationToken)
    {
        try
        {
            return await _dependencyResolver.CheckSingleDependencyAsync(projectPath, "Microsoft.EntityFrameworkCore", cancellationToken);
        }
        catch (Exception)
        {
            // Fallback to text-based search if dependency resolution fails
            return DoesHaveEfCoreReference(projectPath);
        }
    }

    private async Task<bool> DoesHaveEfCoreToolsReferenceAsync(string projectPath, CancellationToken cancellationToken)
    {
        try
        {
            return await _dependencyResolver.HasDirectPackageReferenceAsync(projectPath, "Microsoft.EntityFrameworkCore.Tools", cancellationToken);
        }
        catch (Exception)
        {
            // Fallback to text-based search if dependency resolution fails
            return DoesHaveEfCoreToolsReference(projectPath);
        }
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

    private static bool DoesHaveEfCoreToolsReference(string projectPath)
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

            if (line.Contains("Microsoft.EntityFrameworkCore.Tools"))
            {
                return true;
            }
        }

        return false;
    }
}