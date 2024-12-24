using System.Text.Json;
using YamlDotNet.Serialization;

namespace AbpDevTools.Configuration;

public abstract class DictionaryConfigurationBase<T> : ConfigurationBase<Dictionary<string, T>>
    where T : class
{
    protected DictionaryConfigurationBase(IDeserializer yamlDeserializer, ISerializer yamlSerializer) : base(yamlDeserializer, yamlSerializer)
    {
    }

    protected virtual bool PreserveExistingValues => true;

    public override Dictionary<string, T> GetOptions()
    {
        if (!Directory.Exists(FolderPath))
            Directory.CreateDirectory(FolderPath);

        var options = GetDefaults()!;
        var shouldWrite = !File.Exists(FilePath);

        if (File.Exists(FilePath))
        {
            var existingOptions = base.GetOptions();
            
            if (PreserveExistingValues)
            {
                foreach (var defaultOption in options)
                {
                    if (!existingOptions.ContainsKey(defaultOption.Key))
                    {
                        existingOptions[defaultOption.Key] = defaultOption.Value;
                        shouldWrite = true;
                    }
                }
                options = existingOptions;
            }
            else
            {
                // When not preserving, just add missing defaults to existing options
                options = GetDefaults()!;
                shouldWrite = true;
            }
        }

        if (shouldWrite)
        {
            SaveOptions(options);
        }

        return options;
    }
} 