using System.Runtime.InteropServices;
using System.Text.Json;
using YamlDotNet.Serialization;

namespace AbpDevTools.Configuration;

[RegisterTransient]
public class ToolsConfiguration : ConfigurationBase<ToolOption>
{
    public ToolsConfiguration(IDeserializer yamlDeserializer, ISerializer yamlSerializer) : base(yamlDeserializer, yamlSerializer)
    {
    }

    public override string FileName => "tools-configuration";

    public override ToolOption GetOptions()
    {
        if (!Directory.Exists(FolderPath))
        {
            Directory.CreateDirectory(FolderPath);
        }

        var _defaults = GetDefaults();
        var shouldSave = true;

        if (File.Exists(FilePath))
        {
            var options = ReadOptions()!;

            shouldSave = Merge(options, _defaults);
        }

        if(shouldSave)
        {
            SaveOptions(_defaults);
        }

        return _defaults;
    }

    protected override ToolOption GetDefaults()
    {
        var _defaults = new ToolOption
        {
            { "powershell", "pwsh"},
            { "dotnet", "dotnet" },
            { "abp", "abp" },
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _defaults["open"] = "explorer";
            _defaults["terminal"] = "wt";
        }
        else
        {
            _defaults["open"] = "open";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                _defaults["osascript"] = "osascript";
                _defaults["terminal"] = "terminal";
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
