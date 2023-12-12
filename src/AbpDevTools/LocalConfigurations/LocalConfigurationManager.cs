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

    public bool TryLoad(string filePath, out LocalConfiguration? localConfiguration)
    {
        if (!File.Exists(filePath))
        {
            localConfiguration = null;
            return false;
        }

        var yml = File.ReadAllText(filePath);

        localConfiguration = _deserializer.Deserialize<LocalConfiguration>(yml);

        return true;
    }

    public void ApplyLocalEnvironmentForProcess(string path, ProcessStartInfo process)
    {
        var directory = Path.GetDirectoryName(path);
        if (directory == null)
        {
            return;
        }

        var ymlPath = fileExplorer.FindAscendants(directory, "abpdev.yml").First();

        if (string.IsNullOrEmpty(ymlPath))
        {
            return;
        }

        if (TryLoad(ymlPath, out var localConfiguration))
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