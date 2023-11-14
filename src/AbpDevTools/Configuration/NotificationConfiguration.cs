using System.Text.Json;

namespace AbpDevTools.Configuration;
public static class NotificationConfiguration
{
    public static string FolderPath => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "abpdev");
    public static string FilePath => Path.Combine(FolderPath, "notifications.json");
    public static NotificationOption GetOptions()
    {
        if (!Directory.Exists(FolderPath))
            Directory.CreateDirectory(FolderPath);

        var options = NotificationOption.GetDefaults();
        if (File.Exists(FilePath))
        {
            options = JsonSerializer.Deserialize<NotificationOption>(File.ReadAllText(FilePath));
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

    public static void SetOptions(NotificationOption options)
    {
        File.WriteAllText(FilePath, JsonSerializer.Serialize(options, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }
}

public class NotificationOption
{
    public bool Enabled { get; set; }
    public static NotificationOption GetDefaults() => new NotificationOption();
}