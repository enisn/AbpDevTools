using System.Text.Json;

namespace AbpDevTools.Configuration;
public static class CleanConfiguration
{
    public static string FolderPath => Path.Combine(
           Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
           "abpdev");
    public static string FilePath => Path.Combine(FolderPath, "clean-configuration.json");

    public static CleanOptions GetOptions()
    {
        if (!Directory.Exists(FolderPath))
            Directory.CreateDirectory(FolderPath);

        var options = new CleanOptions();
        if (File.Exists(FilePath))
        {
            options = JsonSerializer.Deserialize<CleanOptions>(File.ReadAllText(FilePath));
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

public class CleanOptions
{
    public string[] Folders { get; set; } = new[]
    {
        "bin",
        "obj",
        "node_modules",
    };
}