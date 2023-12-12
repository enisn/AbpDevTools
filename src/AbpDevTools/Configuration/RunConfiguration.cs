namespace AbpDevTools.Configuration;

[RegisterTransient]
public class RunConfiguration : ConfigurationBase<RunOptions>
{
    public override string FilePath => Path.Combine(FolderPath, "run-configuration.json");

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
}

public class RunOptions
{
    public string[] RunnableProjects { get; set; }
}