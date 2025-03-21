﻿using YamlDotNet.Serialization;

namespace AbpDevTools.Configuration;

[RegisterTransient]
[Obsolete]
public class RunConfiguration : ConfigurationBase<RunOptions>
{
    public RunConfiguration(IDeserializer yamlDeserializer, ISerializer yamlSerializer) : base(yamlDeserializer, yamlSerializer)
    {
    }

    public override string FileName => "run-configuration";

    [Obsolete]
    protected override RunOptions GetDefaults()
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
                ".Web.Public",
                ".Mvc",
                ".Mvc.Host",
                ".Blazor",
                ".Blazor.Host",
                ".Blazor.Server",
                ".Blazor.Server.Host",
                ".Blazor.Server.Tiered",
                ".Unified",
                ".PublicWeb",
                ".PublicWebGateway",
                ".WebGateway"
            }
        };
    }

    [Obsolete]
    public override RunOptions GetOptions()
    {
        return base.GetOptions();
    }

    public void CleanObsolete()
    {
        if(File.Exists(FilePath))
        {
            File.Delete(FilePath);
        }
    }
}

[Obsolete]
public class RunOptions
{
    public string[] RunnableProjects { get; set; } = Array.Empty<string>();
}