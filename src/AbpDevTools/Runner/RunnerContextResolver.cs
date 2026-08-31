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
}
