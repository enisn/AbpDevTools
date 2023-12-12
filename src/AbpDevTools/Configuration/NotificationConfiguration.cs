using System.Text.Json;

namespace AbpDevTools.Configuration;

[RegisterTransient]
public class NotificationConfiguration : ConfigurationBase<NotificationOption>
{
    public override string FilePath => Path.Combine(FolderPath, "notifications.json");

    public void SetOptions(NotificationOption options)
    {
        File.WriteAllText(FilePath, JsonSerializer.Serialize(options, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    protected override NotificationOption GetDefaults() => new();
}

public class NotificationOption
{
    public bool Enabled { get; set; }
}