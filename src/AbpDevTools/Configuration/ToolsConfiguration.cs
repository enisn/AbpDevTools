using System.Runtime.InteropServices;
using System.Text.Json;

namespace AbpDevTools.Configuration;


[RegisterTransient]
public class ToolsConfiguration : ConfigurationBase<ToolOption>
{
    public override string FilePath => Path.Combine(FolderPath, "tools-configuration.json");

    public override ToolOption GetOptions()
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

    protected override ToolOption GetDefaults()
    {
        var _defaults = new ToolOption
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

public class ToolOption : Dictionary<string, string>
{

}
