using System.Text.Json;
using YamlDotNet.Serialization;

namespace AbpDevTools.Configuration;

[RegisterTransient]
public class CleanConfiguration : ConfigurationBase<CleanOptions>
{
    public CleanConfiguration(IDeserializer yamlDeserializer, ISerializer yamlSerializer) : base(yamlDeserializer, yamlSerializer)
    {
    }

    public override string FileName => "clean-configuration";

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