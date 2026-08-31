using AbpDevTools.LocalConfigurations;

namespace AbpDevTools.Runner;

[RegisterTransient]
public sealed class RunnerContextResolver
{
    private readonly LocalConfigurationManager _localConfigurationManager;

    public RunnerContextResolver(LocalConfigurationManager localConfigurationManager)
    {
        _localConfigurationManager = localConfigurationManager;
    }

    public RunnerContextDescriptor Resolve(string? workingDirectory, string? ymlPath = null)
    {
        var resolvedWorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(workingDirectory);

        string? loadedPath = null;
        if (!string.IsNullOrWhiteSpace(ymlPath))
        {
            var explicitPath = Path.GetFullPath(ymlPath);
            if (!_localConfigurationManager.TryLoad(
                    explicitPath,
                    out _,
                    out loadedPath,
                    FileSearchDirection.OnlyCurrent))
            {
                throw new FileNotFoundException($"YAML configuration file '{explicitPath}' was not found.", explicitPath);
            }
        }
        else
        {
            _localConfigurationManager.TryLoad(
                Path.Combine(resolvedWorkingDirectory, "abpdev.yml"),
                out _,
                out loadedPath,
                FileSearchDirection.Ascendants);
        }

        return RunnerContextIdentity.Create(resolvedWorkingDirectory, loadedPath);
    }

    public RunnerContextSnapshot? FindBestActiveContext(
        RunnerContextDescriptor requestedContext,
        IEnumerable<RunnerContextSnapshot> contexts)
    {
        var activeContexts = contexts.Where(x => x.IsActive).ToArray();
        var exactContext = activeContexts.FirstOrDefault(x =>
            string.Equals(x.ContextKey, requestedContext.ContextKey, StringComparison.Ordinal));
        if (exactContext is not null)
        {
            return exactContext;
        }

        var requestedDirectory = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(requestedContext.WorkingDirectory));
        var ancestorContexts = activeContexts
            .Select(x => new
            {
                Context = x,
                WorkingDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(x.WorkingDirectory))
            })
            .Where(x => IsSameOrAncestorDirectory(x.WorkingDirectory, requestedDirectory))
            .OrderByDescending(x => x.WorkingDirectory.Length)
            .ToArray();
        if (ancestorContexts.Length == 0)
        {
            return null;
        }

        var nearestDirectoryLength = ancestorContexts[0].WorkingDirectory.Length;
        var nearestContexts = ancestorContexts
            .Where(x => x.WorkingDirectory.Length == nearestDirectoryLength)
            .ToArray();
        return nearestContexts.Length == 1 ? nearestContexts[0].Context : null;
    }

    private static bool IsSameOrAncestorDirectory(string candidateDirectory, string requestedDirectory)
    {
        var relativePath = Path.GetRelativePath(candidateDirectory, requestedDirectory);
        return relativePath == "." ||
               (!Path.IsPathRooted(relativePath) &&
                relativePath != ".." &&
                !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal));
    }
}
