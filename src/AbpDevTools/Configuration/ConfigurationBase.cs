using System.Text.Json;

namespace AbpDevTools.Configuration;

public interface IConfigurationBase
{
    string FolderPath { get; }

    string FilePath { get; }
}

public abstract class ConfigurationBase<T> : IConfigurationBase
    where T : class
{
    public virtual string FolderPath => Path.Combine(
                  Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                  "abpdev");

    public virtual string FilePath => Path.Combine(
                    FolderPath,
                    GetType().Name +".json");

    public virtual T GetOptions()
    {
        if (!Directory.Exists(FolderPath))
            Directory.CreateDirectory(FolderPath);

        var options = GetDefaults();
        if (File.Exists(FilePath))
        {
            options = JsonSerializer.Deserialize<T>(File.ReadAllText(FilePath));
        }
        else
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(options, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }

        return options;
    }

    protected abstract T GetDefaults();
}
