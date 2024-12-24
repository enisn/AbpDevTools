using System.Text.Json;
using YamlDotNet.Serialization;

namespace AbpDevTools.Configuration;

public interface IConfigurationBase
{
    string FolderPath { get; }

    string FilePath { get; }
}

public abstract class ConfigurationBase<T> : IConfigurationBase
    where T : class
{
    private readonly IDeserializer _yamlDeserializer;
    private readonly ISerializer _yamlSerializer;

    protected ConfigurationBase(IDeserializer yamlDeserializer, ISerializer yamlSerializer)
    {
        _yamlDeserializer = yamlDeserializer;
        _yamlSerializer = yamlSerializer;
    }

    public string FolderPath => Path.Combine(
                  Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                  "abpdev");

    public string FilePath => Path.Combine(
                    FolderPath,
                    FileName + ".yml");

    public virtual string FileName => GetType().Name + ".yml"; 

    protected virtual string LegacyJsonFilePath => Path.Combine(
                FolderPath,
                FileName + ".json");

    public virtual T GetOptions()
    {
        if (!Directory.Exists(FolderPath))
            Directory.CreateDirectory(FolderPath);

        var options = GetDefaults();

        // Check for legacy JSON file and migrate if needed
        if (File.Exists(LegacyJsonFilePath))
        {
            options = MigrateFromJson();
        }
        else if (File.Exists(FilePath))
        {
            options = ReadOptions();
        }
        else
        {
            SaveOptions(options);
        }

        return options;
    }

    protected virtual T ReadOptions()
    {
        var ymlContent = File.ReadAllText(FilePath);
        return _yamlDeserializer.Deserialize<T>(ymlContent);
    }

    protected virtual void SaveOptions(T options)
    {
        var yaml = _yamlSerializer.Serialize(options);
        File.WriteAllText(FilePath, yaml);
    }

    private T MigrateFromJson()
    {
        var jsonContent = File.ReadAllText(LegacyJsonFilePath);
        var options = JsonSerializer.Deserialize<T>(jsonContent)!;
        
        // Save as YAML
        SaveOptions(options);
        
        // Delete old JSON file
        File.Delete(LegacyJsonFilePath);
        
        return options;
    }

    protected abstract T GetDefaults();
}
