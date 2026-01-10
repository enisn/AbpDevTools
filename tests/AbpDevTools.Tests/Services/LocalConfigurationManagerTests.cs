using AbpDevTools.Environments;
using AbpDevTools.LocalConfigurations;
using FluentAssertions;
using NSubstitute;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AbpDevTools.Tests.Services;

/// <summary>
/// Unit tests for LocalConfigurationManager class.
/// Tests project-local abpdev.yml configuration file management including loading,
/// parsing, and environment application functionality.
/// </summary>
public class LocalConfigurationManagerTests : IDisposable
{
    private readonly string _testRootPath;
    private readonly LocalConfigurationManager _manager;
    private readonly IProcessEnvironmentManager _mockEnvironmentManager;
    private readonly IDeserializer _deserializer;
    private readonly ISerializer _serializer;

    public LocalConfigurationManagerTests()
    {
        // Create a temporary directory for test files
        _testRootPath = Path.Combine(Path.GetTempPath(), $"AbpDevTools_LocalConfig_Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testRootPath);

        // Use real deserializer/serializer for proper YAML testing
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        _serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        // Mock only the environment manager which can be mocked
        _mockEnvironmentManager = Substitute.For<IProcessEnvironmentManager>();

        // Create manager with real FileExplorer and YAML serializers
        _manager = new LocalConfigurationManager(
            _deserializer,
            _serializer,
            new FileExplorer(),
            _mockEnvironmentManager);
    }

    public void Dispose()
    {
        // Clean up test directory
        try
        {
            if (Directory.Exists(_testRootPath))
            {
                Directory.Delete(_testRootPath, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    #region TryLoad Tests

    [Fact]
    public void TryLoad_WhenFileDoesNotExist_ReturnsFalseAndNull()
    {
        // Arrange
        var projectPath = Path.Combine(_testRootPath, "MyProject", "MyProject.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        // Note: Don't create the abpdev.yml file

        // Act
        var result = _manager.TryLoad(projectPath, out var localConfiguration);

        // Assert
        result.Should().BeFalse("file does not exist");
        localConfiguration.Should().BeNull("no configuration was loaded");
    }

    [Fact]
    public void TryLoad_WithValidYamlContent_ParsesCorrectly()
    {
        // Arrange
        var projectPath = Path.Combine(_testRootPath, "MyProject", "MyProject.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);

        var yamlContent = @"run:
  watch: true
  noBuild: true
  projects:
    - MyProject.Web
    - MyProject.HttpApi.Host
environment:
  name: Development
  variables:
    ASPNETCORE_ENVIRONMENT: Development
    ConnectionStrings__DefaultConnection: Server=localhost
";

        var configPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "abpdev.yml");
        File.WriteAllText(configPath, yamlContent);

        // Act
        var result = _manager.TryLoad(projectPath, out var localConfiguration);

        // Assert
        result.Should().BeTrue("configuration file exists and is valid");
        localConfiguration.Should().NotBeNull();
        localConfiguration!.Run.Should().NotBeNull();
        localConfiguration.Run.Watch.Should().BeTrue();
        localConfiguration.Run.NoBuild.Should().BeTrue();
        localConfiguration.Run.Projects.Should().HaveCount(2);
        localConfiguration.Run.Projects.Should().Contain("MyProject.Web");
        localConfiguration.Run.Projects.Should().Contain("MyProject.HttpApi.Host");

        localConfiguration.Environment.Should().NotBeNull();
        localConfiguration.Environment.Name.Should().Be("Development");
        localConfiguration.Environment.Variables.Should().HaveCount(2);
        localConfiguration.Environment.Variables.Should().ContainKey("ASPNETCORE_ENVIRONMENT");
        localConfiguration.Environment.Variables["ASPNETCORE_ENVIRONMENT"].Should().Be("Development");
    }

    [Fact]
    public void TryLoad_WithMalformedYaml_HandlesGracefully()
    {
        // Arrange
        var projectPath = Path.Combine(_testRootPath, "MyProject", "MyProject.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);

        var malformedYaml = @"run:
  watch: true
    noBuild: true
  - invalid yaml structure
";

        var configPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "abpdev.yml");
        File.WriteAllText(configPath, malformedYaml);

        // Act & Assert
        // The exception should propagate to allow caller to handle the error
        // SemanticErrorException is a subclass of YamlException
        Assert.ThrowsAny<YamlDotNet.Core.YamlException>(() =>
        {
            _manager.TryLoad(projectPath, out var _);
        });
    }

    [Fact]
    public void TryLoad_WithCustomYmlPath_LoadsFromSpecifiedPath()
    {
        // Arrange
        var customYmlPath = Path.Combine(_testRootPath, "custom-config.yml");
        var projectPath = Path.Combine(_testRootPath, "MyProject", "MyProject.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);

        var yamlContent = @"run:
  watch: true
";
        File.WriteAllText(customYmlPath, yamlContent);

        // Act
        var result = _manager.TryLoad(customYmlPath, out var localConfiguration);

        // Assert
        result.Should().BeTrue("custom YAML path should be used");
        localConfiguration.Should().NotBeNull();
        localConfiguration!.Run.Watch.Should().BeTrue();
    }

    #endregion

    #region Save Tests

    [Fact]
    public void Save_WithValidConfiguration_CreatesYamlFile()
    {
        // Arrange
        var savePath = Path.Combine(_testRootPath, "abpdev.yml");
        var configuration = new LocalConfiguration
        {
            Run = new LocalConfiguration.LocalRunOption
            {
                Watch = true,
                NoBuild = false,
                GraphBuild = true,
                Configuration = "Release",
                SkipMigrate = true,
                Projects = new[] { "MyProject.Web", "MyProject.HttpApi.Host" }
            },
            Environment = new LocalConfiguration.LocalEnvironmentOption
            {
                Name = "Production",
                Variables = new Dictionary<string, string?>
                {
                    { "ASPNETCORE_ENVIRONMENT", "Production" }
                }
            }
        };

        // Act
        var resultPath = _manager.Save(savePath, configuration);

        // Assert
        resultPath.Should().Be(savePath);
        File.Exists(resultPath).Should().BeTrue("YAML file should be created");

        var content = File.ReadAllText(resultPath);
        content.Should().Contain("watch: true");
        content.Should().Contain("noBuild: false");
        content.Should().Contain("graphBuild: true");
        content.Should().Contain("Production");
    }

    [Fact]
    public void Save_WithDirectoryPath_CreatesAbpdevYmlInDirectory()
    {
        // Arrange
        var directoryPath = Path.Combine(_testRootPath, "MyProject");
        Directory.CreateDirectory(directoryPath);

        var configuration = new LocalConfiguration
        {
            Run = new LocalConfiguration.LocalRunOption
            {
                Watch = false
            }
        };

        // Act
        var resultPath = _manager.Save(directoryPath, configuration);

        // Assert
        resultPath.Should().EndWith("abpdev.yml");
        resultPath.Should().Be(Path.Combine(directoryPath, "abpdev.yml"));
        File.Exists(resultPath).Should().BeTrue("YAML file should be created in directory");
    }

    #endregion

    #region ApplyLocalEnvironmentForProcess Tests

    [Fact]
    public void ApplyLocalEnvironmentForProcess_WithEnvironmentName_SetsEnvironment()
    {
        // Arrange
        var projectPath = Path.Combine(_testRootPath, "MyProject", "MyProject.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        var processStartInfo = new System.Diagnostics.ProcessStartInfo();
        var localConfig = new LocalConfiguration
        {
            Environment = new LocalConfiguration.LocalEnvironmentOption
            {
                Name = "Development"
            }
        };

        // Act
        _manager.ApplyLocalEnvironmentForProcess(projectPath, processStartInfo, localConfig);

        // Assert
        _mockEnvironmentManager.Received(1).SetEnvironmentForProcess("Development", processStartInfo);
    }

    [Fact]
    public void ApplyLocalEnvironmentForProcess_WithEnvironmentVariables_SetsVariables()
    {
        // Arrange
        var projectPath = Path.Combine(_testRootPath, "MyProject", "MyProject.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        var processStartInfo = new System.Diagnostics.ProcessStartInfo();
        var variables = new Dictionary<string, string?>
        {
            { "ASPNETCORE_ENVIRONMENT", "Development" },
            { "ConnectionStrings__DefaultConnection", "Server=localhost" }
        };
        var localConfig = new LocalConfiguration
        {
            Environment = new LocalConfiguration.LocalEnvironmentOption
            {
                Variables = variables
            }
        };

        // Act
        _manager.ApplyLocalEnvironmentForProcess(projectPath, processStartInfo, localConfig);

        // Assert
        _mockEnvironmentManager.Received(1).SetEnvironmentVariablesForProcess(processStartInfo, Arg.Is<Dictionary<string, string>>(d =>
            d.Count == 2 &&
            d.ContainsKey("ASPNETCORE_ENVIRONMENT") &&
            d.ContainsKey("ConnectionStrings__DefaultConnection")));
    }

    [Fact]
    public void ApplyLocalEnvironmentForProcess_WithNullConfig_DoesNotSetEnvironment()
    {
        // Arrange
        var projectPath = Path.Combine(_testRootPath, "MyProject", "MyProject.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        var processStartInfo = new System.Diagnostics.ProcessStartInfo();

        // Act
        _manager.ApplyLocalEnvironmentForProcess(projectPath, processStartInfo, null);

        // Assert
        _mockEnvironmentManager.DidNotReceive().SetEnvironmentForProcess(Arg.Any<string>(), Arg.Any<System.Diagnostics.ProcessStartInfo>());
        _mockEnvironmentManager.DidNotReceive().SetEnvironmentVariablesForProcess(Arg.Any<System.Diagnostics.ProcessStartInfo>(), Arg.Any<Dictionary<string, string>>());
    }

    [Fact]
    public void ApplyLocalEnvironmentForProcess_WithEmptyEnvironmentName_DoesNotSetEnvironment()
    {
        // Arrange
        var projectPath = Path.Combine(_testRootPath, "MyProject", "MyProject.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        var processStartInfo = new System.Diagnostics.ProcessStartInfo();
        var localConfig = new LocalConfiguration
        {
            Environment = new LocalConfiguration.LocalEnvironmentOption
            {
                Name = null // No environment name set
            }
        };

        // Act
        _manager.ApplyLocalEnvironmentForProcess(projectPath, processStartInfo, localConfig);

        // Assert
        _mockEnvironmentManager.DidNotReceive().SetEnvironmentForProcess(Arg.Any<string>(), Arg.Any<System.Diagnostics.ProcessStartInfo>());
    }

    #endregion

    #region FileSearchDirection Tests

    [Fact]
    public void TryLoad_WithFileSearchDirectionOnlyCurrent_SearchesOnlyCurrentDirectory()
    {
        // Arrange
        var projectPath = Path.Combine(_testRootPath, "MyProject", "MyProject.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);

        var yamlContent = @"run:
  watch: true
";
        var configPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "abpdev.yml");
        File.WriteAllText(configPath, yamlContent);

        // Also create a file in parent directory to verify it's not found
        var parentYamlPath = Path.Combine(_testRootPath, "abpdev.yml");
        File.WriteAllText(parentYamlPath, @"run:
  watch: false
");

        // Act
        var result = _manager.TryLoad(projectPath, out var localConfiguration, FileSearchDirection.OnlyCurrent);

        // Assert
        result.Should().BeTrue();
        localConfiguration.Should().NotBeNull();
        localConfiguration!.Run.Watch.Should().BeTrue("should load from current directory, not parent");
    }

    #endregion

    #region Configuration Merging Behavior Tests

    [Fact]
    public void LocalConfiguration_ProjectsArray_IsPreservedCorrectly()
    {
        // Arrange
        var projectPath = Path.Combine(_testRootPath, "MyProject", "MyProject.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);

        var yamlContent = @"run:
  projects:
    - ProjectA
    - ProjectB
    - ProjectC
";

        var configPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "abpdev.yml");
        File.WriteAllText(configPath, yamlContent);

        // Act
        var result = _manager.TryLoad(projectPath, out var localConfiguration);

        // Assert
        result.Should().BeTrue();
        localConfiguration.Should().NotBeNull();
        localConfiguration!.Run.Should().NotBeNull();
        localConfiguration.Run.Projects.Should().HaveCount(3);
        localConfiguration.Run.Projects.Should().BeEquivalentTo(new[] { "ProjectA", "ProjectB", "ProjectC" });
    }

    [Fact]
    public void LocalConfiguration_EnvironmentVariables_ArePreservedCorrectly()
    {
        // Arrange
        var projectPath = Path.Combine(_testRootPath, "MyProject", "MyProject.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);

        var yamlContent = @"environment:
  name: Development
  variables:
    KEY1: value1
    KEY2: value2
    KEY3: value3
";

        var configPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "abpdev.yml");
        File.WriteAllText(configPath, yamlContent);

        // Act
        var result = _manager.TryLoad(projectPath, out var localConfiguration);

        // Assert
        result.Should().BeTrue();
        localConfiguration.Should().NotBeNull();
        localConfiguration!.Environment.Should().NotBeNull();
        localConfiguration.Environment.Name.Should().Be("Development");
        localConfiguration.Environment.Variables.Should().HaveCount(3);
        localConfiguration.Environment.Variables.Should().ContainKey("KEY1");
        localConfiguration.Environment.Variables["KEY1"].Should().Be("value1");
        localConfiguration.Environment.Variables["KEY2"].Should().Be("value2");
        localConfiguration.Environment.Variables["KEY3"].Should().Be("value3");
    }

    [Fact]
    public void LocalConfiguration_AllRunOptions_AreParsedCorrectly()
    {
        // Arrange
        var projectPath = Path.Combine(_testRootPath, "MyProject", "MyProject.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);

        var yamlContent = @"run:
  watch: true
  noBuild: true
  graphBuild: true
  configuration: Debug
  skipMigrate: true
  projects:
    - ProjectA
    - ProjectB
";

        var configPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "abpdev.yml");
        File.WriteAllText(configPath, yamlContent);

        // Act
        var result = _manager.TryLoad(projectPath, out var localConfiguration);

        // Assert
        result.Should().BeTrue();
        localConfiguration.Should().NotBeNull();
        localConfiguration!.Run.Should().NotBeNull();
        localConfiguration.Run.Watch.Should().BeTrue();
        localConfiguration.Run.NoBuild.Should().BeTrue();
        localConfiguration.Run.GraphBuild.Should().BeTrue();
        localConfiguration.Run.Configuration.Should().Be("Debug");
        localConfiguration.Run.SkipMigrate.Should().BeTrue();
        localConfiguration.Run.Projects.Should().HaveCount(2);
    }

    [Fact]
    public void TryLoad_InParentDirectory_LoadsFromParent()
    {
        // Arrange
        var projectPath = Path.Combine(_testRootPath, "MyProject", "SubFolder", "MyProject.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);

        // Create config in root test directory (parent of MyProject)
        var yamlContent = @"run:
  watch: true
  configuration: Release
";
        var parentConfigPath = Path.Combine(_testRootPath, "abpdev.yml");
        File.WriteAllText(parentConfigPath, yamlContent);

        // Act
        var result = _manager.TryLoad(projectPath, out var localConfiguration, FileSearchDirection.Ascendants);

        // Assert
        result.Should().BeTrue("should find config in parent directory");
        localConfiguration.Should().NotBeNull();
        localConfiguration!.Run.Watch.Should().BeTrue();
        localConfiguration.Run.Configuration.Should().Be("Release");
    }

    #endregion
}
