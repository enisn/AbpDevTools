using AbpDevTools.Environments;
using System.Diagnostics;
using YamlDotNet.Serialization;

namespace AbpDevTools.LocalConfigurations;

[RegisterTransient]
public class LocalConfigurationManager
{
    protected readonly IDeserializer _deserializer;
    protected readonly FileExplorer fileExplorer;
    protected readonly IProcessEnvironmentManager environmentManager;

    public LocalConfigurationManager(IDeserializer deserializer, FileExplorer fileExplorer, IProcessEnvironmentManager environmentManager)
    {
        _deserializer = deserializer;
        this.fileExplorer = fileExplorer;
        this.environmentManager = environmentManager;
    }

    public bool TryLoad(string path, out LocalConfiguration? localConfiguration, FileSearchDirection direction = FileSearchDirection.Ascendants)
    {
        localConfiguration = null;

        var directory = Path.GetDirectoryName(path);
        if (directory == null)
        {
            return false;
        }

        string? fileName = "abpdev.yml";

        if (path.EndsWith(".yml"))
        {
            fileName = Path.GetFileName(path);
        }

        var ymlPath = direction switch
        {
            FileSearchDirection.Ascendants => fileExplorer.FindAscendants(directory, fileName).FirstOrDefault(),
            FileSearchDirection.Descendants => fileExplorer.FindDescendants(directory, fileName).FirstOrDefault(),
            FileSearchDirection.OnlyCurrent => Path.Combine(directory, fileName),
            _ => throw new NotImplementedException()
        };

        if (string.IsNullOrEmpty(ymlPath) || !File.Exists(ymlPath))
        {
            return false;
        }

        var ymlContent = File.ReadAllText(ymlPath);

        localConfiguration = _deserializer.Deserialize<LocalConfiguration>(ymlContent);

        return true;
    }

    public void ApplyLocalEnvironmentForProcess(string path, ProcessStartInfo process, LocalConfiguration? localConfiguration = null)
    {
        if (localConfiguration is not null || TryLoad(path, out localConfiguration))
        {
            if (!string.IsNullOrEmpty(localConfiguration?.Environment?.Name))
            {
                environmentManager.SetEnvironmentForProcess(
                    localConfiguration.Environment.Name,
                    process);
            }

            if (localConfiguration!.Environment?.Variables != null)
            {
                environmentManager.SetEnvironmentVariablesForProcess(process, localConfiguration!.Environment!.Variables);
            }
        }
    }
}

public enum FileSearchDirection : byte
{
    Ascendants,
    Descendants,
    OnlyCurrent
}