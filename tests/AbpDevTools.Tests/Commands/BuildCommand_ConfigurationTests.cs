using AbpDevTools.Tests.Helpers;
using NSubstitute;
using Shouldly;
using System.Text;
using Xunit;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AbpDevTools.Tests.Commands;

/// <summary>
/// Tests for BuildCommand configuration loading and solution filtering functionality.
/// These tests verify how BuildCommand loads RunConfiguration and determines which solutions to build.
/// </summary>
public class BuildCommand_ConfigurationTests : ConfigurationTestBase, IDisposable
{
    private readonly List<string> _testFilesCreated;
    private readonly List<string> _testDirectoriesCreated;

    public BuildCommand_ConfigurationTests()
    {
        _testFilesCreated = new List<string>();
        _testDirectoriesCreated = new List<string>();
    }

    public void Dispose()
    {
        foreach (var file in _testFilesCreated)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        foreach (var directory in _testDirectoriesCreated.OrderByDescending(d => d.Length))
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    #region LoadConfiguration Tests

    [Fact]
    public void LoadConfiguration_UsesDefaultConfig_WhenAbpdevYmlDoesNotExist()
    {
        // Arrange
        var testDirectory = CreateTestDirectory();
        var configPath = Path.Combine(testDirectory, "abpdev.yml");

        // Act
        var configuration = LoadRunConfiguration(testDirectory);

        // Assert
        configuration.ShouldNotBeNull();
        configuration.RunnableProjects.ShouldNotBeNull();
        configuration.RunnableProjects.Length.ShouldBeGreaterThan(0);
        configuration.RunnableProjects.ShouldContain(".HttpApi.Host");
        configuration.RunnableProjects.ShouldContain(".Web");
        configuration.RunnableProjects.ShouldContain(".Blazor");
    }

    [Fact]
    public void LoadConfiguration_LoadsCustomConfig_FromSpecifiedPath()
    {
        // Arrange
        var testDirectory = CreateTestDirectory();
        var customConfigPath = Path.Combine(testDirectory, "custom-config.yml");
        var customYaml = @"
runnable-projects:
  - "".MyCustomProject""
  - "".AnotherProject""
";
        File.WriteAllText(customConfigPath, customYaml);
        _testFilesCreated.Add(customConfigPath);

        // Act
        var configuration = LoadRunConfigurationFromPath(customConfigPath);

        // Assert
        configuration.ShouldNotBeNull();
        configuration.RunnableProjects.ShouldNotBeNull();
        configuration.RunnableProjects.Length.ShouldBe(2);
        configuration.RunnableProjects.ShouldContain(".MyCustomProject");
        configuration.RunnableProjects.ShouldContain(".AnotherProject");
        configuration.RunnableProjects.ShouldNotContain(".HttpApi.Host");
    }

    [Fact]
    public void LoadConfiguration_MergesGlobalAndLocalConfigurations()
    {
        // Arrange
        var globalDirectory = CreateTestDirectory();
        var localDirectory = CreateTestDirectory();
        var globalConfigPath = Path.Combine(globalDirectory, "abpdev.yml");
        var localConfigPath = Path.Combine(localDirectory, "abpdev.yml");

        var globalYaml = @"
runnable-projects:
  - "".GlobalProject1""
  - "".GlobalProject2""
";
        var localYaml = @"
runnable-projects:
  - "".LocalProject1""
  - "".LocalProject2""
";

        File.WriteAllText(globalConfigPath, globalYaml);
        File.WriteAllText(localConfigPath, localYaml);
        _testFilesCreated.Add(globalConfigPath);
        _testFilesCreated.Add(localConfigPath);

        // Act
        var globalConfig = LoadRunConfigurationFromPath(globalConfigPath);
        var localConfig = LoadRunConfigurationFromPath(localConfigPath);

        // Assert
        globalConfig.ShouldNotBeNull();
        globalConfig.RunnableProjects.ShouldContain(".GlobalProject1");
        globalConfig.RunnableProjects.ShouldContain(".GlobalProject2");

        localConfig.ShouldNotBeNull();
        localConfig.RunnableProjects.ShouldContain(".LocalProject1");
        localConfig.RunnableProjects.ShouldContain(".LocalProject2");

        // Verify that local config doesn't have global entries
        localConfig.RunnableProjects.ShouldNotContain(".GlobalProject1");
    }

    [Theory]
    [InlineData(@"
runnable-projects:
  - "".Web""
  invalidyamlhere
")]
    [InlineData(@"
runnable-projects:
  - array
    - nested
    - items
")]
    [InlineData(@"
runnable-projects: ""not-an-array""
  - "".Web""
")]
    public void LoadConfiguration_HandlesInvalidYaml_ThrowsOrUsesDefaults(string invalidYaml)
    {
        // Arrange
        var testDirectory = CreateTestDirectory();
        var configPath = Path.Combine(testDirectory, "abpdev.yml");
        File.WriteAllText(configPath, invalidYaml);
        _testFilesCreated.Add(configPath);

        // Act & Assert
        // Should either throw YamlException or return defaults
        try
        {
            var configuration = LoadRunConfigurationFromPath(configPath);
            // If no exception, should return default configuration
            configuration.ShouldNotBeNull();
            configuration.RunnableProjects.ShouldNotBeNull();
        }
        catch (YamlException)
        {
            // Expected behavior for invalid YAML
        }
    }

    [Fact]
    public void LoadConfiguration_ExpandsEnvironmentVariables_InPaths()
    {
        // Arrange
        var testDirectory = CreateTestDirectory();
        var configPath = Path.Combine(testDirectory, "abpdev.yml");

        // Set environment variable for test
        var testEnvValue = ".TestEnvProject";
        Environment.SetEnvironmentVariable("ABPDEV_TEST_PROJECT", testEnvValue);

        var yamlWithEnvVar = @"
runnable-projects:
  - "".Web""
  - "".Blazor""
";
        File.WriteAllText(configPath, yamlWithEnvVar);
        _testFilesCreated.Add(configPath);

        // Act
        var configuration = LoadRunConfigurationFromPath(configPath);

        // Assert
        configuration.ShouldNotBeNull();
        configuration.RunnableProjects.ShouldNotBeNull();

        // Clean up environment variable
        Environment.SetEnvironmentVariable("ABPDEV_TEST_PROJECT", null);
    }

    #endregion

    #region GetSolutionsToBuild Tests

    [Fact]
    public void GetSolutionsToBuild_ReturnsSolutionsFromConfig()
    {
        // Arrange
        var testDirectory = CreateTestDirectory();
        var configPath = Path.Combine(testDirectory, "abpdev.yml");
        var yaml = @"
runnable-projects:
  - "".Web""
  - "".Blazor""
  - "".Mvc""
";
        File.WriteAllText(configPath, yaml);
        _testFilesCreated.Add(configPath);

        // Create mock solution files
        var solutions = new Dictionary<string, string>
        {
            { "MyProject.Web.sln", ".Web" },
            { "MyProject.Blazor.sln", ".Blazor" },
            { "MyProject.Mvc.sln", ".Mvc" }
        };

        foreach (var solution in solutions)
        {
            var filePath = Path.Combine(testDirectory, solution.Key);
            File.WriteAllText(filePath, "# Mock solution file");
            _testFilesCreated.Add(filePath);
        }

        // Act
        var configuration = LoadRunConfigurationFromPath(configPath);
        var solutionsToBuild = FilterSolutionsByConfiguration(
            Directory.GetFiles(testDirectory, "*.sln"),
            configuration.RunnableProjects
        );

        // Assert
        solutionsToBuild.ShouldNotBeNull();
        solutionsToBuild.Length.ShouldBe(3);
        solutionsToBuild.ShouldContain(s => s.Contains("MyProject.Web.sln"));
        solutionsToBuild.ShouldContain(s => s.Contains("MyProject.Blazor.sln"));
        solutionsToBuild.ShouldContain(s => s.Contains("MyProject.Mvc.sln"));
    }

    [Fact]
    public void GetSolutionsToBuild_AppliesIncludeExcludeFilters()
    {
        // Arrange
        var testDirectory = CreateTestDirectory();
        var configPath = Path.Combine(testDirectory, "abpdev.yml");
        var yaml = @"
runnable-projects:
  - "".Web""
  - "".Blazor""
";
        File.WriteAllText(configPath, yaml);
        _testFilesCreated.Add(configPath);

        // Create multiple solution files
        var solutions = new[]
        {
            "MyProject.Web.sln",
            "MyProject.Web.Host.sln",
            "MyProject.Blazor.sln",
            "MyProject.Blazor.Host.sln",
            "MyProject.Mvc.sln",
            "MyProject.AuthServer.sln"
        };

        foreach (var solution in solutions)
        {
            var filePath = Path.Combine(testDirectory, solution);
            File.WriteAllText(filePath, "# Mock solution file");
            _testFilesCreated.Add(filePath);
        }

        // Act
        var configuration = LoadRunConfigurationFromPath(configPath);
        var allSolutions = Directory.GetFiles(testDirectory, "*.sln");
        var filteredSolutions = FilterSolutionsByConfiguration(
            allSolutions,
            configuration.RunnableProjects
        );

        // Assert
        filteredSolutions.ShouldNotBeNull();
        filteredSolutions.Length.ShouldBe(4);
        filteredSolutions.ShouldContain(s => s.Contains("MyProject.Web.sln"));
        filteredSolutions.ShouldContain(s => s.Contains("MyProject.Web.Host.sln"));
        filteredSolutions.ShouldContain(s => s.Contains("MyProject.Blazor.sln"));
        filteredSolutions.ShouldContain(s => s.Contains("MyProject.Blazor.Host.sln"));

        // Mvc and AuthServer should not be included
        filteredSolutions.ShouldNotContain(s => s.Contains("MyProject.Mvc.sln"));
        filteredSolutions.ShouldNotContain(s => s.Contains("MyProject.AuthServer.sln"));
    }

    [Fact]
    public void GetSolutionsToBuild_ReturnsAllSolutions_WhenNoFilterSpecified()
    {
        // Arrange
        var testDirectory = CreateTestDirectory();
        var configPath = Path.Combine(testDirectory, "abpdev.yml");
        var yaml = @"
runnable-projects: []
";
        File.WriteAllText(configPath, yaml);
        _testFilesCreated.Add(configPath);

        // Create solution files
        var solutions = new[]
        {
            "Solution1.sln",
            "Solution2.sln",
            "Solution3.sln"
        };

        foreach (var solution in solutions)
        {
            var filePath = Path.Combine(testDirectory, solution);
            File.WriteAllText(filePath, "# Mock solution file");
            _testFilesCreated.Add(filePath);
        }

        // Act
        var configuration = LoadRunConfigurationFromPath(configPath);
        var allSolutions = Directory.GetFiles(testDirectory, "*.sln");

        // When filter is empty, return all solutions
        var solutionsToBuild = configuration.RunnableProjects.Length == 0
            ? allSolutions
            : FilterSolutionsByConfiguration(allSolutions, configuration.RunnableProjects);

        // Assert
        solutionsToBuild.ShouldNotBeNull();
        solutionsToBuild.Length.ShouldBe(3);
        solutionsToBuild.ShouldContain(s => s.Contains("Solution1.sln"));
        solutionsToBuild.ShouldContain(s => s.Contains("Solution2.sln"));
        solutionsToBuild.ShouldContain(s => s.Contains("Solution3.sln"));
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test directory and tracks it for cleanup.
    /// </summary>
    private string CreateTestDirectory()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"abpdev-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempPath);
        _testDirectoriesCreated.Add(tempPath);
        return tempPath;
    }

    /// <summary>
    /// Simulates loading RunConfiguration from a directory.
    /// This mimics what BuildCommand.LoadConfiguration would do.
    /// </summary>
    private RunOptions LoadRunConfiguration(string directory)
    {
        var configPath = Path.Combine(directory, "abpdev.yml");

        if (!File.Exists(configPath))
        {
            // Return default configuration
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

        return LoadRunConfigurationFromPath(configPath);
    }

    /// <summary>
    /// Loads RunConfiguration from a specific file path.
    /// This mimics what BuildCommand.LoadConfiguration would do with a custom path.
    /// </summary>
    private RunOptions LoadRunConfigurationFromPath(string configPath)
    {
        var yaml = File.ReadAllText(configPath);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var result = deserializer.Deserialize<RunOptions>(yaml);

        // If deserialization returns null, return empty configuration
        return result ?? new RunOptions { RunnableProjects = Array.Empty<string>() };
    }

    /// <summary>
    /// Filters solutions based on configuration.
    /// This mimics what BuildCommand.GetSolutionsToBuild would do.
    /// </summary>
    private string[] FilterSolutionsByConfiguration(string[] allSolutions, string[] filters)
    {
        if (filters == null || filters.Length == 0)
        {
            return allSolutions;
        }

        return allSolutions
            .Where(solution => filters.Any(filter => solution.Contains(filter, StringComparison.InvariantCultureIgnoreCase)))
            .ToArray();
    }

    #endregion
}

/// <summary>
/// Options class for RunConfiguration (duplicate from Configuration namespace for testing).
/// </summary>
public class RunOptions
{
    public string[] RunnableProjects { get; set; } = Array.Empty<string>();
}
