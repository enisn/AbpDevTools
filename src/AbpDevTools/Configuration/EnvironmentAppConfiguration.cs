using YamlDotNet.Serialization;

namespace AbpDevTools.Configuration;

[RegisterTransient]
public class EnvironmentAppConfiguration : DictionaryConfigurationBase<EnvironmentToolOption>
{
    public const string SqlServer = "sqlserver";
    public const string SqlServerEdge = "sqlserver-edge";
    public const string PostgreSql = "postgresql";
    public const string MySql = "mysql";
    public const string MongoDb = "mongodb";
    public const string Redis = "redis";
    public const string RabbitMq = "rabbitmq";

    public EnvironmentAppConfiguration(IDeserializer yamlDeserializer, ISerializer yamlSerializer) : base(yamlDeserializer, yamlSerializer)
    {
    }

    public override string FileName => "environment-tools";

    protected override Dictionary<string, EnvironmentToolOption> GetDefaults()
    {
        return new Dictionary<string, EnvironmentToolOption>
        {
            { SqlServer, new EnvironmentToolOption(
                new[] 
                { 
                    "docker start tmp-sqlserver", 
                    "docker run --name tmp-sqlserver --restart unless-stopped -e \"ACCEPT_EULA=Y\" -e \"SA_PASSWORD=Passw0rd\" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2017-CU8-ubuntu" 
                }, 
                new[] { "docker kill tmp-sqlserver", "docker rm tmp-sqlserver" }) },
            { SqlServerEdge, new EnvironmentToolOption(
                new[] 
                { 
                    "docker start tmp-sqlserver-edge", 
                    "docker run --name tmp-sqlserver-edge --restart unless-stopped -d --cap-add SYS_PTRACE -e \"ACCEPT_EULA=1\" -e \"MSSQL_SA_PASSWORD=Passw0rd\" -p 1433:1433 mcr.microsoft.com/azure-sql-edge" 
                }, 
                new[] { "docker kill tmp-sqlserver-edge", "docker rm tmp-sqlserver-edge" }) },
            { PostgreSql, new EnvironmentToolOption(
                new[] 
                { 
                    "docker start tmp-postgres", 
                    "docker run --name tmp-postgres --restart unless-stopped -e POSTGRES_PASSWORD=Passw0rd -p 5432:5432 -d postgres" 
                }, 
                new[] { "docker kill tmp-posgres", "docker rm tmp-posgres" }) },
            { MySql, new EnvironmentToolOption(
                new[] 
                { 
                    "docker start tmp-mysql", 
                    "docker run --name tmp-mysql --restart unless-stopped -e \"MYSQL_ROOT_PASSWORD=Passw0rd\" -p 3306:3306 --platform linux/x86_64 -d mysql:5.7" 
                }, 
                new[] { "docker kill tmp-mysql", "docker rm tmp-mysql" }) },
            { MongoDb, new EnvironmentToolOption(
                new[] 
                { 
                    "docker start tmp-mongo", 
                    "docker run --name tmp-mongo --restart unless-stopped -p 27017:27017 -d mongo:latest" 
                }, 
                new[] { "docker kill tmp-mongo", "docker rm tmp-mongo" }) },
            { Redis, new EnvironmentToolOption(
                new[] 
                { 
                    "docker start tmp-redis", 
                    "docker run --name tmp-redis -p 6379:6379 -d --restart unless-stopped redis" 
                }, 
                new[] { "docker kill tmp-redis", "docker rm tmp-redis" }) },
            { RabbitMq, new EnvironmentToolOption(
                new[] 
                { 
                    "docker start tmp-rabbitmq", 
                    "docker run --name tmp-rabbitmq -d --restart unless-stopped -p 15672:15672 -p 5672:5672 rabbitmq:3-management" 
                }, 
                new[] { "docker kill tmp-rabbitmq", "docker rm tmp-rabbitmq" }) }
        };
    }
}

public class EnvironmentToolOption
{
    private string[]? _startCmds;
    private string[]? _stopCmds;

    public EnvironmentToolOption()
    {
    }

    public EnvironmentToolOption(string[] startCmds, string[] stopCmds)
    {
        _startCmds = startCmds;
        _stopCmds = stopCmds;
    }

    // Legacy string properties for backward compatibility with old YAML files
    [YamlMember(Alias = "StartCmd")]
    public string? StartCmd 
    { 
        get => _startCmds != null ? string.Join(" || ", _startCmds) : null;
        set 
        {
            if (!string.IsNullOrEmpty(value))
            {
                // Migrate old format: split by || or ;
                _startCmds = value.Contains(" || ") 
                    ? value.Split(" || ", StringSplitOptions.TrimEntries) 
                    : value.Split(';', StringSplitOptions.TrimEntries);
            }
        }
    }

    [YamlMember(Alias = "StopCmd")]
    public string? StopCmd 
    { 
        get => _stopCmds != null ? string.Join(";", _stopCmds) : null;
        set 
        {
            if (!string.IsNullOrEmpty(value))
            {
                // Migrate old format: split by ;
                _stopCmds = value.Split(';', StringSplitOptions.TrimEntries);
            }
        }
    }

    // New array properties (preferred)
    [YamlMember(Alias = "StartCmds")]
    public string[] StartCmds 
    { 
        get => _startCmds ?? Array.Empty<string>();
        set => _startCmds = value;
    }

    [YamlMember(Alias = "StopCmds")]
    public string[] StopCmds 
    { 
        get => _stopCmds ?? Array.Empty<string>();
        set => _stopCmds = value;
    }
}
