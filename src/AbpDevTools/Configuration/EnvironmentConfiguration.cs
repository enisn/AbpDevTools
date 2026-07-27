using System.Text.Json;
using YamlDotNet.Serialization;

namespace AbpDevTools.Configuration;

[RegisterTransient]
public class EnvironmentConfiguration : DictionaryConfigurationBase<EnvironmentOption>
{
    public const string SqlServer = "SqlServer";
    public const string PostgreSql = "PostgreSql";
    public const string MySql = "MySql";
    public const string MongoDb = "MongoDb";
    
    public EnvironmentConfiguration(IDeserializer yamlDeserializer, ISerializer yamlSerializer) : base(yamlDeserializer, yamlSerializer)
    {
    }

    protected override Dictionary<string, EnvironmentOption> GetDefaults()
    {
        return new Dictionary<string, EnvironmentOption>
        {
            {
                SqlServer, new EnvironmentOption
                {
                    Variables = new Dictionary<string, string>
                    {
                        { "ConnectionStrings__Default", $"Server=localhost;Database={{AppName}}_{{Today}};User ID=SA;Password={EnvironmentAppConfiguration.SqlServerDefaultPassword};TrustServerCertificate=True" }
                    }
                }
            },
            {
                MongoDb, new EnvironmentOption
                {
                    Variables = new Dictionary<string, string>
                    {
                            { "ConnectionStrings__Default", "mongodb://localhost:27017/{AppName}_{Today}" }
                        }
                    }
            },
            {
                PostgreSql, new EnvironmentOption
                {
                    Variables = new Dictionary<string, string>
                    {
                        { "ConnectionStrings__Default", $"Server=localhost;Port=5432;Database={{AppName}}_{{Today}};User Id=postgres;Password={EnvironmentAppConfiguration.PostgreSqlDefaultPassword};" }
                    }
                }
            },
            {
                MySql, new EnvironmentOption
                {
                    Variables = new Dictionary<string, string>
                    {
                        { "ConnectionStrings__Default", $"Server=localhost;Port=3306;Database={{AppName}}_{{Today}};User Id=root;Password={EnvironmentAppConfiguration.MySqlDefaultPassword};" }
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
