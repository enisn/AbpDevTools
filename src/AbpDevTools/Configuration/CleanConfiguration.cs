using System.Text.Json;

namespace AbpDevTools.Configuration;

[RegisterTransient]
public class CleanConfiguration : ConfigurationBase<CleanOptions>
{
    public override string FilePath => Path.Combine(FolderPath, "clean-configuration.json");

    protected override CleanOptions GetDefaults()
    {
        return new();
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