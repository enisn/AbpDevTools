using System.Text.Json;

namespace AbpDevTools.Configuration;
public static class EnvironmentAppConfiguration
{
    public static string FolderPath => Path.Combine(
               Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
               "abpdev");
    public static string FilePath => Path.Combine(FolderPath, "environment-tools.json");
    public static Dictionary<string, EnvironmentToolOption> GetOptions()
    {
        if (!Directory.Exists(FolderPath))
            Directory.CreateDirectory(FolderPath);

        var options = EnvironmentToolOption.GetDefaults();
        if (File.Exists(FilePath))
        {
            options = JsonSerializer.Deserialize<Dictionary<string, EnvironmentToolOption>>(File.ReadAllText(FilePath));
        }
        else
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(options, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }

        return options;
    }
}

public class EnvironmentToolOption
{
    public EnvironmentToolOption()
    {
    }

    public EnvironmentToolOption(string startCmd, string stopCmd)
    {
        StartCmd = startCmd;
        StopCmd = stopCmd;
    }

    public string StartCmd { get; set; }
    public string StopCmd { get; set; }

    public static Dictionary<string, EnvironmentToolOption> GetDefaults()
    {
        return new Dictionary<string, EnvironmentToolOption>
        {
            { "sqlserver", new EnvironmentToolOption("docker run --name tmp-sqlserver --restart unless-stopped -e \"ACCEPT_EULA=Y\" -e \"SA_PASSWORD=Passw0rd\" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2017-CU8-ubuntu", "docker kill tmp-sqlserver;docker rm tmp-sqlserver") },
            { "sqlserver-edge", new EnvironmentToolOption("docker run --name tmp-sqlserver --restart unless-stopped -d --cap-add SYS_PTRACE -e \"ACCEPT_EULA=1\" -e \"MSSQL_SA_PASSWORD=Passw0rd\" -p 1433:1433 mcr.microsoft.com/azure-sql-edge", "docker kill tmp-sqlserver;docker rm tmp-sqlserver") },
            { "postgresql", new EnvironmentToolOption("docker run --name tmp-postgres --restart unless-stopped -e POSTGRES_PASSWORD=Passw0rd -p 5432:5432 -d postgres", "docker kill tmp-posgres;docker rm tmp-posgres") },
            { "mysql", new  EnvironmentToolOption("docker run --name tmp-mysql --restart unless-stopped -e \"MYSQL_ROOT_PASSWORD=Passw0rd\" -p 3306:3306 --platform linux/x86_64 -d mysql:5.7", "docker kill tmp-mysql;docker rm tmp-mysql" )},
            { "mongodb", new EnvironmentToolOption("docker run --name tmp-mongo --restart unless-stopped -p 27017:27017 -d mongo:latest","docker kill tmp-mongo;docker rm tmp-mongo")},
            { "redis", new EnvironmentToolOption("docker run --name tmp-redis -p 6379:6379 -d --restart unless-stopped redis", "docker kill tmp-redis;docker rm tmp-redis") },
            { "rabbitmq", new EnvironmentToolOption("docker run --name tmp-rabbitmq -d --restart unless-stopped -p 15672:15672 -p 5672:5672 rabbitmq:3-management", "docker kill tmp-rabbitmq;docker rm tmp-rabbitmq") }
        };
    }
}
