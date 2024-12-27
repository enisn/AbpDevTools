using AbpDevTools.Environments;
using System.Diagnostics;
using YamlDotNet.Serialization;

namespace AbpDevTools.LocalConfigurations;

[RegisterTransient]
public class LocalConfigurationManager
{
    protected readonly IDeserializer _deserializer;
    protected readonly ISerializer _serializer;
    protected readonly FileExplorer fileExplorer;
    protected readonly IProcessEnvironmentManager environmentManager;

    public LocalConfigurationManager(IDeserializer deserializer, ISerializer serializer, FileExplorer fileExplorer, IProcessEnvironmentManager environmentManager)
    {
        _deserializer = deserializer;
        _serializer = serializer;
        this.fileExplorer = fileExplorer;
        this.environmentManager = environmentManager;
    }

    public string Save(string path, LocalConfiguration configuration)
    {
        var directory = Path.GetDirectoryName(path);
        if (directory == null)
        {
            throw new ArgumentException("Invalid path", nameof(path));
        }

        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var yaml = _serializer.Serialize(configuration);
        
        var filePath = path;
        if (!path.EndsWith(".yml"))
        {
            filePath = Path.Combine(path, "abpdev.yml");
        }

        File.WriteAllText(filePath, yaml);
        return filePath;
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