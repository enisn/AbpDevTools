using AbpDevTools.Configuration;
using AbpDevTools.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace AbpDevTools.Tests.Configuration;

/// <summary>
/// Unit tests for <see cref="EnvironmentAppConfiguration"/> class.
/// Tests YAML deserialization of Docker container app configurations including
/// SQL Server, Redis, MongoDB, PostgreSQL, RabbitMQ, and other container types.
/// </summary>
public class EnvironmentAppConfigurationTests : ConfigurationTestBase
{
    #region Valid YAML Deserialization Tests

    [Fact]
    public void Deserialize_ValidYaml_WithMultipleAppConfigurations_ShouldSucceed()
    {
        // Arrange
        var yaml = @"
sql-server:
  start-cmds:
    - docker start tmp-sqlserver
    - docker run --name tmp-sqlserver --restart unless-stopped -e ""ACCEPT_EULA=Y"" -e ""SA_PASSWORD=Passw0rd"" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2017-CU8-ubuntu
  stop-cmds:
    - docker kill tmp-sqlserver
    - docker rm tmp-sqlserver

redis:
  start-cmds:
    - docker start tmp-redis
    - docker run --name tmp-redis -p 6379:6379 -d --restart unless-stopped redis
  stop-cmds:
    - docker kill tmp-redis
    - docker rm tmp-redis

mongodb:
  start-cmds:
    - docker start tmp-mongo
    - docker run --name tmp-mongo --restart unless-stopped -p 27017:27017 -d mongo:latest
  stop-cmds:
    - docker kill tmp-mongo
    - docker rm tmp-mongo

postgres:
  start-cmds:
    - docker start tmp-postgres
    - docker run --name tmp-postgres --restart unless-stopped -e POSTGRES_PASSWORD=Passw0rd -p 5432:5432 -d postgres
  stop-cmds:
    - docker kill tmp-postgres
    - docker rm tmp-postgres

rabbitmq:
  start-cmds:
    - docker start tmp-rabbitmq
    - docker run --name tmp-rabbitmq -d --restart unless-stopped -p 15672:15672 -p 5672:5672 rabbitmq:3-management
  stop-cmds:
    - docker kill tmp-rabbitmq
    - docker rm tmp-rabbitmq
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentToolOption>>(yaml);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(5);

        result.Should().ContainKey("sql-server");
        result.Should().ContainKey("redis");
        result.Should().ContainKey("mongodb");
        result.Should().ContainKey("postgres");
        result.Should().ContainKey("rabbitmq");
    }

    [Fact]
    public void Deserialize_ValidYaml_WithNewArrayFormat_ShouldParseStartCmdsCorrectly()
    {
        // Arrange
        var yaml = @"
mysql:
  start-cmds:
    - docker start tmp-mysql
    - docker run --name tmp-mysql --restart unless-stopped -e ""MYSQL_ROOT_PASSWORD=Passw0rd"" -p 3306:3306 --platform linux/x86_64 -d mysql:5.7
  stop-cmds:
    - docker kill tmp-mysql
    - docker rm tmp-mysql
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentToolOption>>(yaml);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainKey("mysql");

        var mysqlConfig = result["mysql"];
        mysqlConfig.StartCmds.Should().HaveCount(2);
        mysqlConfig.StartCmds[0].Should().Be("docker start tmp-mysql");
        mysqlConfig.StartCmds[1].Should().Contain("docker run --name tmp-mysql");
        mysqlConfig.StartCmds[1].Should().Contain("MYSQL_ROOT_PASSWORD=Passw0rd");
        mysqlConfig.StartCmds[1].Should().Contain("-p 3306:3306");

        mysqlConfig.StopCmds.Should().HaveCount(2);
        mysqlConfig.StopCmds[0].Should().Be("docker kill tmp-mysql");
        mysqlConfig.StopCmds[1].Should().Be("docker rm tmp-mysql");
    }

    [Fact]
    public void Deserialize_ValidYaml_WithLegacyStringFormat_ShouldMigrateToArrayFormat()
    {
        // Arrange
        var yaml = @"
legacy-app:
  start-cmd: docker start legacy-app || docker run --name legacy-app -d nginx:latest
  stop-cmd: docker kill legacy-app;docker rm legacy-app
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentToolOption>>(yaml);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainKey("legacy-app");

        var config = result["legacy-app"];
        config.StartCmds.Should().HaveCount(2);
        config.StartCmds[0].Should().Be("docker start legacy-app");
        config.StartCmds[1].Should().Be("docker run --name legacy-app -d nginx:latest");

        config.StopCmds.Should().HaveCount(2);
        config.StopCmds[0].Should().Be("docker kill legacy-app");
        config.StopCmds[1].Should().Be("docker rm legacy-app");
    }

    #endregion

    #region Port Settings Tests

    [Theory]
    [InlineData("1433:1433", "sql-server")]
    [InlineData("6379:6379", "redis")]
    [InlineData("27017:27017", "mongodb")]
    [InlineData("5432:5432", "postgres")]
    [InlineData("3306:3306", "mysql")]
    [InlineData("5672:5672", "rabbitmq")]
    public void Deserialize_ValidYaml_WithPortMappings_ShouldParseCorrectly(string portMapping, string appName)
    {
        // Arrange
        var yaml = $@"
{appName}:
  start-cmds:
    - docker run --name tmp-{appName} -p {portMapping} -d {appName}:latest
  stop-cmds:
    - docker kill tmp-{appName}
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentToolOption>>(yaml);

        // Assert
        result.Should().NotBeNull();
        result[appName].StartCmds[0].Should().Contain($"-p {portMapping}");
    }

    [Fact]
    public void Deserialize_ValidYaml_WithMultiplePorts_ShouldPreserveAllPortMappings()
    {
        // Arrange
        var yaml = @"
rabbitmq:
  start-cmds:
    - docker run --name tmp-rabbitmq -d --restart unless-stopped -p 15672:15672 -p 5672:5672 -p 25672:25672 rabbitmq:3-management
  stop-cmds:
    - docker kill tmp-rabbitmq
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentToolOption>>(yaml);

        // Assert
        result.Should().NotBeNull();
        var startCmd = result["rabbitmq"].StartCmds[0];
        startCmd.Should().Contain("-p 15672:15672");
        startCmd.Should().Contain("-p 5672:5672");
        startCmd.Should().Contain("-p 25672:25672");
    }

    [Fact]
    public void Deserialize_ValidYaml_WithCustomPortMappings_ShouldPreserveCustomPorts()
    {
        // Arrange
        var yaml = @"
custom-redis:
  start-cmds:
    - docker run --name custom-redis -p 6380:6379 -d redis:latest
  stop-cmds:
    - docker kill custom-redis
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentToolOption>>(yaml);

        // Assert
        result.Should().NotBeNull();
        result["custom-redis"].StartCmds[0].Should().Contain("-p 6380:6379");
    }

    #endregion

    #region Image Names and Versions Tests

    [Theory]
    [InlineData("mcr.microsoft.com/mssql/server:2017-CU8-ubuntu", "sql-server")]
    [InlineData("redis:latest", "redis")]
    [InlineData("mongo:latest", "mongodb")]
    [InlineData("postgres:15", "postgres")]
    [InlineData("mysql:5.7", "mysql")]
    [InlineData("rabbitmq:3-management", "rabbitmq")]
    [InlineData("mcr.microsoft.com/azure-sql-edge", "sqlserver-edge")]
    public void Deserialize_ValidYaml_WithImageNameAndVersion_ShouldParseCorrectly(string image, string appName)
    {
        // Arrange
        var yaml = $@"
{appName}:
  start-cmds:
    - docker run --name tmp-{appName} -d {image}
  stop-cmds:
    - docker kill tmp-{appName}
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentToolOption>>(yaml);

        // Assert
        result.Should().NotBeNull();
        result[appName].StartCmds[0].Should().Contain(image);
    }

    [Fact]
    public void Deserialize_ValidYaml_WithMultipleImageVersions_ShouldPreserveAllVersions()
    {
        // Arrange
        var yaml = @"
postgres-14:
  start-cmds:
    - docker run --name postgres-14 -d postgres:14
  stop-cmds:
    - docker kill postgres-14

postgres-15:
  start-cmds:
    - docker run --name postgres-15 -d postgres:15
  stop-cmds:
    - docker kill postgres-15

postgres-16:
  start-cmds:
    - docker run --name postgres-16 -d postgres:16
  stop-cmds:
    - docker kill postgres-16
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentToolOption>>(yaml);

        // Assert
        result.Should().HaveCount(3);
        result["postgres-14"].StartCmds[0].Should().Contain("postgres:14");
        result["postgres-15"].StartCmds[0].Should().Contain("postgres:15");
        result["postgres-16"].StartCmds[0].Should().Contain("postgres:16");
    }

    #endregion

    #region Environment Variables Tests

    [Theory]
    [InlineData("ACCEPT_EULA=Y", "sql-server")]
    [InlineData("SA_PASSWORD=Passw0rd", "sql-server")]
    [InlineData("POSTGRES_PASSWORD=Passw0rd", "postgres")]
    [InlineData("MYSQL_ROOT_PASSWORD=Passw0rd", "mysql")]
    [InlineData("MSSQL_SA_PASSWORD=Passw0rd", "sqlserver-edge")]
    public void Deserialize_ValidYaml_WithEnvironmentVariables_ShouldParseCorrectly(string envVar, string appName)
    {
        // Arrange
        var yaml = $@"
{appName}:
  start-cmds:
    - docker run --name tmp-{appName} -e ""{envVar}"" -d {appName}:latest
  stop-cmds:
    - docker kill tmp-{appName}
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentToolOption>>(yaml);

        // Assert
        result.Should().NotBeNull();
        result[appName].StartCmds[0].Should().Contain($"-e \"{envVar}\"");
    }

    [Fact]
    public void Deserialize_ValidYaml_WithMultipleEnvironmentVariables_ShouldPreserveAll()
    {
        // Arrange
        var yaml = @"
sql-server:
  start-cmds:
    - docker run --name tmp-sqlserver --restart unless-stopped -e ""ACCEPT_EULA=Y"" -e ""SA_PASSWORD=Passw0rd"" -e ""MSSQL_PID=Developer"" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2017-CU8-ubuntu
  stop-cmds:
    - docker kill tmp-sqlserver
    - docker rm tmp-sqlserver
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentToolOption>>(yaml);

        // Assert
        result.Should().NotBeNull();
        var startCmd = result["sql-server"].StartCmds[0];
        startCmd.Should().Contain("-e \"ACCEPT_EULA=Y\"");
        startCmd.Should().Contain("-e \"SA_PASSWORD=Passw0rd\"");
        startCmd.Should().Contain("-e \"MSSQL_PID=Developer\"");
    }

    [Fact]
    public void Deserialize_ValidYaml_WithComplexEnvironmentVariables_ShouldPreserveCorrectly()
    {
        // Arrange
        var yaml = @"
app:
  start-cmds:
    - docker run --name my-app -e ""ConnectionStrings__Default=Server=localhost;Database=MyDb;User ID=SA;Password=Pass123;"" -e ""ASPNETCORE_ENVIRONMENT=Development"" -d myapp:latest
  stop-cmds:
    - docker kill my-app
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentToolOption>>(yaml);

        // Assert
        result.Should().NotBeNull();
        result["app"].StartCmds[0].Should().Contain("ConnectionStrings__Default=Server=localhost;Database=MyDb;User ID=SA;Password=Pass123;");
        result["app"].StartCmds[0].Should().Contain("ASPNETCORE_ENVIRONMENT=Development");
    }

    #endregion

    #region Enabled/Disabled State Tests

    [Fact]
    public void Deserialize_ValidYaml_WithAllSupportedAppTypes_ShouldContainAllDefaults()
    {
        // Arrange
        var yaml = @"
sqlserver:
  start-cmds:
    - docker start tmp-sqlserver
  stop-cmds:
    - docker kill tmp-sqlserver

postgresql:
  start-cmds:
    - docker start tmp-postgres
  stop-cmds:
    - docker kill tmp-postgres

mysql:
  start-cmds:
    - docker start tmp-mysql
  stop-cmds:
    - docker kill tmp-mysql

mongodb:
  start-cmds:
    - docker start tmp-mongo
  stop-cmds:
    - docker kill tmp-mongo

redis:
  start-cmds:
    - docker start tmp-redis
  stop-cmds:
    - docker kill tmp-redis

rabbitmq:
  start-cmds:
    - docker start tmp-rabbitmq
  stop-cmds:
    - docker kill tmp-rabbitmq

sqlserver-edge:
  start-cmds:
    - docker start tmp-sqlserver-edge
  stop-cmds:
    - docker kill tmp-sqlserver-edge
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentToolOption>>(yaml);

        // Assert
        result.Should().HaveCount(7);
        result.Should().ContainKey("sqlserver");
        result.Should().ContainKey("postgresql");
        result.Should().ContainKey("mysql");
        result.Should().ContainKey("mongodb");
        result.Should().ContainKey("redis");
        result.Should().ContainKey("rabbitmq");
        result.Should().ContainKey("sqlserver-edge");
    }

    [Fact]
    public void Deserialize_ValidYaml_WithPartialConfiguration_ShouldOnlyContainDefinedApps()
    {
        // Arrange
        var yaml = @"
redis:
  start-cmds:
    - docker start tmp-redis
  stop-cmds:
    - docker kill tmp-redis

mongodb:
  start-cmds:
    - docker start tmp-mongo
  stop-cmds:
    - docker kill tmp-mongo
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentToolOption>>(yaml);

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainKey("redis");
        result.Should().ContainKey("mongodb");
        result.Should().NotContainKey("sqlserver");
        result.Should().NotContainKey("postgres");
    }

    #endregion

    #region Volume Mappings Tests

    [Fact]
    public void Deserialize_ValidYaml_WithVolumeMappings_ShouldPreserveVolumePaths()
    {
        // Arrange
        var yaml = @"
postgres-with-volume:
  start-cmds:
    - docker run --name tmp-postgres -v /data/postgres:/var/lib/postgresql/data -p 5432:5432 -d postgres:15
  stop-cmds:
    - docker kill tmp-postgres
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentToolOption>>(yaml);

        // Assert
        result.Should().NotBeNull();
        result["postgres-with-volume"].StartCmds[0].Should().Contain("-v /data/postgres:/var/lib/postgresql/data");
    }

    [Fact]
    public void Deserialize_ValidYaml_WithMultipleVolumeMappings_ShouldPreserveAllVolumes()
    {
        // Arrange
        var yaml = @"
mongodb-with-volumes:
  start-cmds:
    - docker run --name tmp-mongo -v /data/mongo/data:/data/db -v /data/mongo/config:/data/configdb -p 27017:27017 -d mongo:latest
  stop-cmds:
    - docker kill tmp-mongo
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentToolOption>>(yaml);

        // Assert
        result.Should().NotBeNull();
        var startCmd = result["mongodb-with-volumes"].StartCmds[0];
        startCmd.Should().Contain("-v /data/mongo/data:/data/db");
        startCmd.Should().Contain("-v /data/mongo/config:/data/configdb");
    }

    [Fact]
    public void Deserialize_ValidYaml_WithWindowsVolumePaths_ShouldPreserveBackslashes()
    {
        // Arrange
        var yaml = @"
sql-server-with-volume:
  start-cmds:
    - docker run --name tmp-sqlserver -v C:\\Data\\SQLServer:/var/opt/mssql -p 1433:1433 -d mcr.microsoft.com/mssql/server:2017-CU8-ubuntu
  stop-cmds:
    - docker kill tmp-sqlserver
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentToolOption>>(yaml);

        // Assert
        result.Should().NotBeNull();
        var startCmd = result["sql-server-with-volume"].StartCmds[0];
        startCmd.Should().Contain("-v C:");
        startCmd.Should().Contain("Data");
        startCmd.Should().Contain("SQLServer:/var/opt/mssql");
    }

    #endregion

    #region Empty and Null Configuration Tests

    [Fact]
    public void Deserialize_EmptyYaml_ShouldReturnNull()
    {
        // Arrange
        var yaml = @"# Empty environment tools configuration
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentToolOption>>(yaml);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Deserialize_ValidYaml_WithEmptyCommands_ShouldDeserializeWithEmptyArrays()
    {
        // Arrange
        var yaml = @"
empty-app:
  start-cmds: []
  stop-cmds: []
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentToolOption>>(yaml);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainKey("empty-app");
        result["empty-app"].StartCmds.Should().BeEmpty();
        result["empty-app"].StopCmds.Should().BeEmpty();
    }

    #endregion

    #region Default Values Tests

    [Fact]
    public void EnvironmentAppConfiguration_ShouldHaveSqlServerConstant()
    {
        // Arrange & Act
        var constant = EnvironmentAppConfiguration.SqlServer;

        // Assert
        constant.Should().NotBeNull();
        constant.Should().Be("sqlserver");
    }

    [Fact]
    public void EnvironmentAppConfiguration_ShouldHaveSqlServerEdgeConstant()
    {
        // Arrange & Act
        var constant = EnvironmentAppConfiguration.SqlServerEdge;

        // Assert
        constant.Should().NotBeNull();
        constant.Should().Be("sqlserver-edge");
    }

    [Fact]
    public void EnvironmentAppConfiguration_ShouldHavePostgreSqlConstant()
    {
        // Arrange & Act
        var constant = EnvironmentAppConfiguration.PostgreSql;

        // Assert
        constant.Should().NotBeNull();
        constant.Should().Be("postgresql");
    }

    [Fact]
    public void EnvironmentAppConfiguration_ShouldHaveMySqlConstant()
    {
        // Arrange & Act
        var constant = EnvironmentAppConfiguration.MySql;

        // Assert
        constant.Should().NotBeNull();
        constant.Should().Be("mysql");
    }

    [Fact]
    public void EnvironmentAppConfiguration_ShouldHaveMongoDbConstant()
    {
        // Arrange & Act
        var constant = EnvironmentAppConfiguration.MongoDb;

        // Assert
        constant.Should().NotBeNull();
        constant.Should().Be("mongodb");
    }

    [Fact]
    public void EnvironmentAppConfiguration_ShouldHaveRedisConstant()
    {
        // Arrange & Act
        var constant = EnvironmentAppConfiguration.Redis;

        // Assert
        constant.Should().NotBeNull();
        constant.Should().Be("redis");
    }

    [Fact]
    public void EnvironmentAppConfiguration_ShouldHaveRabbitMqConstant()
    {
        // Arrange & Act
        var constant = EnvironmentAppConfiguration.RabbitMq;

        // Assert
        constant.Should().NotBeNull();
        constant.Should().Be("rabbitmq");
    }

    #endregion

    #region Special Characters and Escaping Tests

    [Fact]
    public void Deserialize_ValidYaml_WithSpecialCharactersInCommands_ShouldPreserveCorrectly()
    {
        // Arrange
        var yaml = @"
app-with-special-chars:
  start-cmds:
    - docker run --name my-app -e ""PASSWORD=P@ssw0rd!#$%"" -e ""CONNECTION=Server=localhost;Database=MyDb;"" -d myapp:latest
  stop-cmds:
    - docker kill my-app
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentToolOption>>(yaml);

        // Assert
        result.Should().NotBeNull();
        result["app-with-special-chars"].StartCmds[0].Should().Contain("PASSWORD=P@ssw0rd!#$%");
        result["app-with-special-chars"].StartCmds[0].Should().Contain("CONNECTION=Server=localhost;Database=MyDb;");
    }

    #endregion

    #region Restart Policy Tests

    [Fact]
    public void Deserialize_ValidYaml_WithRestartPolicy_ShouldPreserveRestartFlag()
    {
        // Arrange
        var yaml = @"
app-with-restart:
  start-cmds:
    - docker run --name my-app --restart unless-stopped -d nginx:latest
  stop-cmds:
    - docker kill my-app
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentToolOption>>(yaml);

        // Assert
        result.Should().NotBeNull();
        result["app-with-restart"].StartCmds[0].Should().Contain("--restart unless-stopped");
    }

    [Theory]
    [InlineData("no")]
    [InlineData("always")]
    [InlineData("on-failure")]
    [InlineData("unless-stopped")]
    public void Deserialize_ValidYaml_WithVariousRestartPolicies_ShouldParseCorrectly(string restartPolicy)
    {
        // Arrange
        var yaml = $@"
app-{restartPolicy}:
  start-cmds:
    - docker run --name my-app --restart {restartPolicy} -d nginx:latest
  stop-cmds:
    - docker kill my-app
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentToolOption>>(yaml);

        // Assert
        result.Should().NotBeNull();
        result[$"app-{restartPolicy}"].StartCmds[0].Should().Contain($"--restart {restartPolicy}");
    }

    #endregion

    #region Platform Tests

    [Fact]
    public void Deserialize_ValidYaml_WithPlatformFlag_ShouldPreservePlatform()
    {
        // Arrange
        var yaml = @"
mysql-with-platform:
  start-cmds:
    - docker run --name tmp-mysql --restart unless-stopped -e ""MYSQL_ROOT_PASSWORD=Passw0rd"" -p 3306:3306 --platform linux/x86_64 -d mysql:5.7
  stop-cmds:
    - docker kill tmp-mysql
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentToolOption>>(yaml);

        // Assert
        result.Should().NotBeNull();
        result["mysql-with-platform"].StartCmds[0].Should().Contain("--platform linux/x86_64");
    }

    #endregion

    #region Command Array Format Tests

    [Fact]
    public void EnvironmentToolOption_StartCmdsProperty_ShouldReturnEmptyArrayWhenNotSet()
    {
        // Arrange & Act
        var option = new EnvironmentToolOption();

        // Assert
        option.StartCmds.Should().NotBeNull();
        option.StartCmds.Should().BeEmpty();
    }

    [Fact]
    public void EnvironmentToolOption_StopCmdsProperty_ShouldReturnEmptyArrayWhenNotSet()
    {
        // Arrange & Act
        var option = new EnvironmentToolOption();

        // Assert
        option.StopCmds.Should().NotBeNull();
        option.StopCmds.Should().BeEmpty();
    }

    [Fact]
    public void EnvironmentToolOption_ConstructorWithParameters_ShouldSetCommandsCorrectly()
    {
        // Arrange
        var startCmds = new[] { "docker start app", "docker run --name app -d image" };
        var stopCmds = new[] { "docker kill app", "docker rm app" };

        // Act
        var option = new EnvironmentToolOption(startCmds, stopCmds);

        // Assert
        option.StartCmds.Should().BeEquivalentTo(startCmds);
        option.StopCmds.Should().BeEquivalentTo(stopCmds);
    }

    #endregion
}
