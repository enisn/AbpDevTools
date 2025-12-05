namespace AbpDevTools.Services;

[RegisterSingleton]
public class EntityFrameworkCoreProjectsProvider
{
    private readonly DotnetDependencyResolver _dependencyResolver;

    public EntityFrameworkCoreProjectsProvider(DotnetDependencyResolver dependencyResolver)
    {
        _dependencyResolver = dependencyResolver;
    }

    public async Task<FileInfo[]> GetEfCoreProjectsAsync(string path, string[]? projectFilters = null, CancellationToken cancellationToken = default)
    {
        var projects = Directory.EnumerateFiles(path, "*.csproj", SearchOption.AllDirectories);
        
        // Apply project filters early if provided to avoid scanning unnecessary projects
        if (projectFilters != null && projectFilters.Length > 0)
        {
            projects = projects.Where(p => projectFilters.Any(filter => p.Contains(filter)));
        }
        
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

    /// <summary>
    /// Gets projects that have EF Core Tools installed (can run dotnet ef commands).
    /// This checks for Microsoft.EntityFrameworkCore.Tools package reference,
    /// which is required for design-time operations like migrations and database drops.
    /// </summary>
    public async Task<FileInfo[]> GetEfCoreToolsProjectsAsync(string path, CancellationToken cancellationToken = default)
    {
        var projects = Directory.EnumerateFiles(path, "*.csproj", SearchOption.AllDirectories);
        var efCoreToolsProjects = new List<FileInfo>();

        foreach (var projectPath in projects)
        {
            if (await DoesHaveEfCoreToolsReferenceAsync(projectPath, cancellationToken))
            {
                efCoreToolsProjects.Add(new FileInfo(projectPath));
            }
        }

        return efCoreToolsProjects.ToArray();
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
            return DoesHaveEfCoreReferenceInProjectFile(projectPath);
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
            return DoesHaveEfCoreToolsReferenceInProjectFile(projectPath);
        }
    }

    private static bool DoesHaveEfCoreReferenceInProjectFile(string projectPath)
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

    private static bool DoesHaveEfCoreToolsReferenceInProjectFile(string projectPath)
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