using System.Text.Json;

namespace AbpDevTools.Configuration;
public static class EnvironmentConfiguration
{
    public static string FolderPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "abpdev");

    public static string FilePath => Path.Combine(FolderPath, "environments.json");

    public static Dictionary<string, EnvironmentOption> GetOptions()
    {
        if (!Directory.Exists(FolderPath))
            Directory.CreateDirectory(FolderPath);

        var options = EnvironmentOption.GetDefaults();
        if (File.Exists(FilePath))
        {
            options = JsonSerializer.Deserialize<Dictionary<string, EnvironmentOption>>(File.ReadAllText(FilePath));
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
}

public class EnvironmentOption
{
    public Dictionary<string, string> Variables { get; set; }

    public static Dictionary<string, EnvironmentOption> GetDefaults() =>
        new Dictionary<string, EnvironmentOption>
        {
            {
                "SqlServer", new EnvironmentOption
                {
                    Variables = new Dictionary<string, string>
                    {
                        { "ConnectionStrings__Default", "Server=localhost;Database={AppName}_{Today};User ID=SA;Password=12345678Aa;TrustServerCertificate=True" }
                    }
                }
            },
            {
                 "MongoDB", new EnvironmentOption
                    {
                        Variables = new Dictionary<string, string>
                        {
                            { "ConnectionStrings__Default", "mongodb://localhost:27017/{AppName}_{Today}" }
                        }
                    }
            }
        };
}