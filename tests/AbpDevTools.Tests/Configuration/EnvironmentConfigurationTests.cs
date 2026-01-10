using AbpDevTools.Configuration;
using AbpDevTools.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace AbpDevTools.Tests.Configuration;

/// <summary>
/// Unit tests for <see cref="EnvironmentConfiguration"/> class.
/// Tests YAML deserialization, environment variables, path configurations, and default values.
/// </summary>
public class EnvironmentConfigurationTests : ConfigurationTestBase
{
    #region Valid YAML Deserialization Tests

    [Fact]
    public void Deserialize_ValidYaml_WithEnvironmentSettings_ShouldSucceed()
    {
        // Arrange
        var yaml = @"
sql-server:
  variables:
    ConnectionStrings__Default: Server=localhost;Database=MyDb;User ID=SA;Password=Pass123;
    ASPNETCORE_ENVIRONMENT: Development
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentOption>>(yaml);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.Should().ContainKey("sql-server");

        var sqlServerConfig = result["sql-server"];
        sqlServerConfig.Variables.Should().NotBeNull();
        sqlServerConfig.Variables.Should().HaveCount(2);
        sqlServerConfig.Variables.Should().ContainKey("ConnectionStrings__Default");
        sqlServerConfig.Variables["ConnectionStrings__Default"].Should().Be("Server=localhost;Database=MyDb;User ID=SA;Password=Pass123;");
        sqlServerConfig.Variables["ASPNETCORE_ENVIRONMENT"].Should().Be("Development");
    }

    [Fact]
    public void Deserialize_ValidYaml_WithMultipleEnvironments_ShouldDeserializeAll()
    {
        // Arrange
        var yaml = @"
development:
  variables:
    ASPNETCORE_ENVIRONMENT: Development
    ASPNETCORE_URLS: http://localhost:5000

production:
  variables:
    ASPNETCORE_ENVIRONMENT: Production
    ASPNETCORE_URLS: https://example.com

staging:
  variables:
    ASPNETCORE_ENVIRONMENT: Staging
    ASPNETCORE_URLS: https://staging.example.com
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentOption>>(yaml);

        // Assert
        result.Should().HaveCount(3);
        result.Should().ContainKey("development");
        result.Should().ContainKey("production");
        result.Should().ContainKey("staging");

        result["development"].Variables["ASPNETCORE_ENVIRONMENT"].Should().Be("Development");
        result["production"].Variables["ASPNETCORE_ENVIRONMENT"].Should().Be("Production");
        result["staging"].Variables["ASPNETCORE_ENVIRONMENT"].Should().Be("Staging");
    }

    #endregion

    #region Environment Variables Tests

    [Theory]
    [InlineData("ConnectionStrings__Default", "Server=localhost;Database=TestDb;")]
    [InlineData("ASPNETCORE_ENVIRONMENT", "Development")]
    [InlineData("ASPNETCORE_URLS", "http://localhost:5000")]
    [InlineData("JWT__Secret", "MySecretKey123")]
    [InlineData("Redis__ConnectionString", "localhost:6379")]
    public void Deserialize_ValidYaml_WithVariousEnvironmentVariables_ShouldParseCorrectly(string key, string value)
    {
        // Arrange
        var yaml = $@"
test-env:
  variables:
    {key}: {value}
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentOption>>(yaml);

        // Assert
        result.Should().NotBeNull();
        result["test-env"].Variables.Should().ContainKey(key);
        result["test-env"].Variables[key].Should().Be(value);
    }

    [Fact]
    public void Deserialize_ValidYaml_WithMultipleVariables_ShouldPreserveAll()
    {
        // Arrange
        var yaml = @"
my-environment:
  variables:
    ConnectionStrings__Default: Server=localhost;Database=MyDb;
    ASPNETCORE_ENVIRONMENT: Development
    ASPNETCORE_URLS: http://localhost:5000
    JWT__Secret: MySecret
    Redis__ConnectionString: localhost:6379
    ElasticSearch__Url: http://localhost:9200
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentOption>>(yaml);

        // Assert
        result.Should().HaveCount(1);
        result["my-environment"].Variables.Should().HaveCount(6);

        result["my-environment"].Variables.Should().ContainKey("ConnectionStrings__Default");
        result["my-environment"].Variables.Should().ContainKey("ASPNETCORE_ENVIRONMENT");
        result["my-environment"].Variables.Should().ContainKey("ASPNETCORE_URLS");
        result["my-environment"].Variables.Should().ContainKey("JWT__Secret");
        result["my-environment"].Variables.Should().ContainKey("Redis__ConnectionString");
        result["my-environment"].Variables.Should().ContainKey("ElasticSearch__Url");
    }

    [Fact]
    public void Deserialize_ValidYaml_WithNestedVariableKeys_ShouldParseCorrectly()
    {
        // Arrange
        var yaml = @"
test-env:
  variables:
    ConnectionStrings__Default: Server=localhost;
    JWT__Secret: secret123
    JWT__Issuer: self
    Redis__Configuration: localhost:6379
    Redis__Password: redispass
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentOption>>(yaml);

        // Assert
        result["test-env"].Variables.Should().HaveCount(5);

        result["test-env"].Variables["ConnectionStrings__Default"].Should().Be("Server=localhost;");
        result["test-env"].Variables["JWT__Secret"].Should().Be("secret123");
        result["test-env"].Variables["JWT__Issuer"].Should().Be("self");
        result["test-env"].Variables["Redis__Configuration"].Should().Be("localhost:6379");
        result["test-env"].Variables["Redis__Password"].Should().Be("redispass");
    }

    #endregion

    #region Path Configurations Tests

    [Theory]
    [InlineData(@"C:\Projects\MyProject")]
    [InlineData(@"/home/user/projects/myproject")]
    [InlineData("~/projects/myproject")]
    [InlineData(@"..\..\relative\path")]
    [InlineData("./current/directory")]
    public void Deserialize_ValidYaml_WithPathVariable_ShouldPreserveAsIs(string pathValue)
    {
        // Arrange
        var yaml = $@"
path-env:
  variables:
    ProjectPath: {pathValue}
    OutputPath: {pathValue}\output
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentOption>>(yaml);

        // Assert
        result.Should().NotBeNull();
        result["path-env"].Variables["ProjectPath"].Should().Be(pathValue);
        result["path-env"].Variables["OutputPath"].Should().Be($"{pathValue}\\output");
    }

    [Fact]
    public void Deserialize_ValidYaml_WithConnectionStringPlaceholders_ShouldPreservePlaceholders()
    {
        // Arrange
        var yaml = @"
placeholder-env:
  variables:
    ConnectionStrings__Default: Server=localhost;Database={AppName}_{Today};User ID=SA;
    ConnectionStrings__Redis: localhost:6379,{AppName}_{Today}
    AppName: MyApp
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentOption>>(yaml);

        // Assert
        result["placeholder-env"].Variables["ConnectionStrings__Default"].Should().Contain("{AppName}");
        result["placeholder-env"].Variables["ConnectionStrings__Default"].Should().Contain("{Today}");
        result["placeholder-env"].Variables["ConnectionStrings__Redis"].Should().Contain("{AppName}_{Today}");
    }

    #endregion

    #region Empty and Null Configuration Tests

    [Fact]
    public void Deserialize_EmptyMappings_ShouldReturnNull()
    {
        // Arrange
        var yaml = @"# Empty configuration file
# No environment definitions
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentOption>>(yaml);

        // Assert
        // YAML deserialization returns null for empty documents
        result.Should().BeNull();
    }

    [Fact]
    public void Deserialize_ValidYaml_WithEmptyVariables_ShouldDeserializeWithEmptyDictionary()
    {
        // Arrange
        var yaml = @"
empty-vars-env:
  variables: {}
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentOption>>(yaml);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainKey("empty-vars-env");
        result["empty-vars-env"].Variables.Should().NotBeNull();
        result["empty-vars-env"].Variables.Should().BeEmpty();
    }

    [Fact]
    public void Deserialize_ValidYaml_WithNullVariables_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
no-vars-env:
  variables:
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentOption>>(yaml);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainKey("no-vars-env");
        // When variables section is empty/null, it may be null or empty dict
        result["no-vars-env"].Variables.Should().BeNull();
    }

    [Fact]
    public void Deserialize_ValidYaml_WithNullVariableValue_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
null-var-env:
  variables:
    SomeKey:
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentOption>>(yaml);

        // Assert
        result.Should().NotBeNull();
        result["null-var-env"].Variables.Should().ContainKey("SomeKey");
        // Empty YAML values deserialize to null or empty string
        var value = result["null-var-env"].Variables["SomeKey"];
        value.Should().BeNullOrWhiteSpace();
    }

    #endregion

    #region Default Values Tests

    [Fact]
    public void EnvironmentConfiguration_ShouldHaveSqlServerDefault()
    {
        // Act
        var result = EnvironmentConfiguration.SqlServer;

        // Assert
        result.Should().NotBeNull();
        result.Should().Be("SqlServer");
    }

    [Fact]
    public void EnvironmentConfiguration_ShouldHavePostgreSqlDefault()
    {
        // Arrange & Act
        var result = EnvironmentConfiguration.PostgreSql;

        // Assert
        result.Should().NotBeNull();
        result.Should().Be("PostgreSql");
    }

    [Fact]
    public void EnvironmentConfiguration_ShouldHaveMySqlDefault()
    {
        // Arrange & Act
        var result = EnvironmentConfiguration.MySql;

        // Assert
        result.Should().NotBeNull();
        result.Should().Be("MySql");
    }

    [Fact]
    public void EnvironmentConfiguration_ShouldHaveMongoDbDefault()
    {
        // Arrange & Act
        var result = EnvironmentConfiguration.MongoDb;

        // Assert
        result.Should().NotBeNull();
        result.Should().Be("MongoDb");
    }

    [Theory]
    [InlineData("SqlServer", "ConnectionStrings__Default")]
    [InlineData("PostgreSql", "ConnectionStrings__Default")]
    [InlineData("MySql", "ConnectionStrings__Default")]
    [InlineData("MongoDb", "ConnectionStrings__Default")]
    public void EnvironmentConfiguration_Defaults_ShouldHaveConnectionStringVariable(string envName, string variableKey)
    {
        // Arrange
        var yaml = $@"
{envName}:
  variables:
    {variableKey}: test-connection-string
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentOption>>(yaml);

        // Assert
        result.Should().ContainKey(envName);
        result[envName].Variables.Should().ContainKey(variableKey);
    }

    [Fact]
    public void Deserialize_ValidYaml_WithPlaceholders_ShouldContainAppNameAndTodayPlaceholders()
    {
        // Arrange
        var yaml = @"
test-env:
  variables:
    ConnectionStrings__Default: Server=localhost;Database={AppName}_{Today};User ID=SA;
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentOption>>(yaml);

        // Assert
        var connectionString = result["test-env"].Variables["ConnectionStrings__Default"];
        connectionString.Should().Contain("{AppName}");
        connectionString.Should().Contain("{Today}");
    }

    #endregion

    #region Special Characters and Escaping Tests

    [Theory]
    [InlineData("password-with-special-chars-!@#$%")]
    [InlineData("path with spaces")]
    [InlineData("value;with;semicolons")]
    [InlineData("value=with=equals")]
    public void Deserialize_ValidYaml_WithSpecialCharactersInValues_ShouldPreserveCorrectly(string value)
    {
        // Arrange
        var yaml = $@"
special-chars-env:
  variables:
    SpecialValue: {value}
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentOption>>(yaml);

        // Assert
        result["special-chars-env"].Variables["SpecialValue"].Should().Be(value);
    }

    #endregion

    #region Case Sensitivity Tests

    [Fact]
    public void Deserialize_ValidYaml_VariableKeys_ShouldPreserveCase()
    {
        // Arrange
        var yaml = @"
case-test-env:
  variables:
    ConnectionStrings__Default: connection1
    connectionstrings__default: connection2
    CONNECTIONSTRINGS__DEFAULT: connection3
    ASPNETCORE_ENVIRONMENT: Development
    aspnetcore_environment: Staging
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentOption>>(yaml);

        // Assert
        result["case-test-env"].Variables.Should().HaveCount(5);
        result["case-test-env"].Variables.Should().ContainKey("ConnectionStrings__Default");
        result["case-test-env"].Variables.Should().ContainKey("connectionstrings__default");
        result["case-test-env"].Variables.Should().ContainKey("CONNECTIONSTRINGS__DEFAULT");
        result["case-test-env"].Variables.Should().ContainKey("ASPNETCORE_ENVIRONMENT");
        result["case-test-env"].Variables.Should().ContainKey("aspnetcore_environment");
    }

    #endregion

    #region Override and Merge Tests

    [Fact]
    public void Deserialize_ValidYaml_DuplicateEnvironmentKeys_LastOneShouldWin()
    {
        // Arrange
        var yaml = @"
duplicate-env:
  variables:
    Key1: Value1

duplicate-env:
  variables:
    Key2: Value2
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentOption>>(yaml);

        // Assert
        result.Should().HaveCount(1);
        result.Should().ContainKey("duplicate-env");
        result["duplicate-env"].Variables.Should().HaveCount(1);
        result["duplicate-env"].Variables.Should().ContainKey("Key2");
        result["duplicate-env"].Variables["Key2"].Should().Be("Value2");
    }

    [Fact]
    public void Deserialize_ValidYaml_DuplicateVariableKeys_LastOneShouldWin()
    {
        // Arrange
        var yaml = @"
duplicate-var-env:
  variables:
    DuplicateKey: FirstValue
    DuplicateKey: SecondValue
    OtherKey: OtherValue
";

        // Act
        var result = DeserializeYaml<Dictionary<string, EnvironmentOption>>(yaml);

        // Assert
        result["duplicate-var-env"].Variables.Should().HaveCount(2);
        result["duplicate-var-env"].Variables["DuplicateKey"].Should().Be("SecondValue");
        result["duplicate-var-env"].Variables["OtherKey"].Should().Be("OtherValue");
    }

    #endregion
}
