using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace AbpDevTools.Configuration;

[RegisterTransient]
public class LocalSourcesConfiguration : ConfigurationBase<LocalSourceMapping>
{
    public override string FileName => "local-sources";

    public LocalSourcesConfiguration(IDeserializer yamlDeserializer, ISerializer yamlSerializer) : base(yamlDeserializer, yamlSerializer)
    {
    }

    protected override LocalSourceMapping GetDefaults()
    {
        return new LocalSourceMapping(){
            { "abp", new LocalSourceMappingItem
                {
                    RemotePath = "https://github.com/abpframework/abp.git",
                    Path = "C:\\github\\abp",
                    Packages = new HashSet<string>
                    {
                        "Volo.Abp.*"
                    }
                }
            }
        };
    }
}

public class LocalSourceMapping : Dictionary<string, LocalSourceMappingItem>
{
}

public class LocalSourceMappingItem
{
    public string Path { get; set; } = string.Empty;
    public string? RemotePath { get; set; }
    public HashSet<string> Packages { get; set; } = new();
}