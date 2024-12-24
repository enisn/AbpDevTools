using System.Text.Json;

namespace AbpDevTools.Configuration;

[RegisterTransient]
public class EnvironmentConfiguration : DictionaryConfigurationBase<EnvironmentOption>
{
    protected override Dictionary<string, EnvironmentOption> GetDefaults()
    {
        return new Dictionary<string, EnvironmentOption>
        {
            {
                "SqlServer", new EnvironmentOption
                {
                    Variables = new Dictionary<string, string>
                    {
                        { "ConnectionStrings__Default", "Server=localhost;Database={AppName}_{Today};User ID=SA;Password=12345678Aa;TrustServerCertificate=True" }
                    }
                }
            },
            {
                 "MongoDB", new EnvironmentOption
                    {
                        Variables = new Dictionary<string, string>
                        {
                            { "ConnectionStrings__Default", "mongodb://localhost:27017/{AppName}_{Today}" }
                        }
                    }
            },
            {
                "Postres", new EnvironmentOption
                {
                    Variables = new Dictionary<string, string>
                    {
                        { "ConnectionStrings__Default", "Server=localhost;Port=5432;Database={AppName}_{Today};User Id=postgres;Password=12345678Aa;" }
                    }
                }
            }
        };
    }
}

public class EnvironmentOption
{
    public Dictionary<string, string> Variables { get; set; }
}