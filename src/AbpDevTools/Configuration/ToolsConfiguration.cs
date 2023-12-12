using System.Runtime.InteropServices;
using System.Text.Json;

namespace AbpDevTools.Configuration;
public static class ToolsConfiguration
{
    public static string FolderPath => Path.Combine(
       Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
       "abpdev");
    public static string FilePath => Path.Combine(FolderPath, "tools-configuration.json");

    public static Dictionary<string, string> GetOptions()
    {
        if (!Directory.Exists(FolderPath))
            Directory.CreateDirectory(FolderPath);

        var _defaults = GetDefaults();
        var shouldSave = true;

        if (File.Exists(FilePath))
        {
            var options = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(FilePath));

            shouldSave = Merge(options, _defaults);
        }

        if(shouldSave)
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_defaults, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }

        return _defaults;
    }

    private static Dictionary<string, string> GetDefaults()
    {
        var _defaults = new Dictionary<string, string>
        {
            { "powershell", "pwsh"},
            { "dotnet", "dotnet" },
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _defaults["open"] = "explorer";
        }
        else
        {
            _defaults["open"] = "open";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                _defaults["osascript"] = "osascript";
            }
        }

        return _defaults;
    }

    private static bool Merge(Dictionary<string, string> options, Dictionary<string, string> defaults)
    {
        var changed = false;
        foreach (var (key, value) in defaults)
        {
            if (!options.ContainsKey(key))
            {
                options[key] = value;
                changed = true;
            }
        }

        return changed;
    }
}
