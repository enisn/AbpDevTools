using System.Text.Json;

namespace AbpDevTools.Configuration;
public static class RunConfiguration
{
    public static string FolderPath => Path.Combine(
           Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
           "abpdev");
    public static string FilePath => Path.Combine(FolderPath, "run-configuration.json");

    public static RunOptions GetOptions()
    {
        if (!Directory.Exists(FolderPath))
            Directory.CreateDirectory(FolderPath);

        var options = RunOptions.GetDefaults();
        if (File.Exists(FilePath))
        {
            options = JsonSerializer.Deserialize<RunOptions>(File.ReadAllText(FilePath));
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

public class RunOptions
{
    public string[] RunnableProjects { get; set; }

    public static RunOptions GetDefaults()
    {
        return new RunOptions
        {
            RunnableProjects = new[]
            {
                ".HttpApi.Host",
                ".HttpApi.HostWithIds",
                ".AuthServer",
                ".IdentityServer",
                ".Web",
                ".Web.Host",
                ".Mvc",
                ".Mvc.Host",
                ".Blazor",
                ".Blazor.Host",
                ".Blazor.Server",
                ".Blazor.Server.Host",
                ".Blazor.Server.Tiered",
                ".Unified",
            }
        };
    }
}