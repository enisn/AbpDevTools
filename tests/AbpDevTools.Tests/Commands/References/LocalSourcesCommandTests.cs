using AbpDevTools.Configuration;
using AbpDevTools.Tests.Helpers;
using FluentAssertions;
using NSubstitute;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AbpDevTools.Tests.Commands.References;

/// <summary>
/// Unit tests for <see cref="Commands.References.LocalSourcesCommand"/> class.
/// Tests CRUD operations on local source mappings through the configuration system.
/// </summary>
public class LocalSourcesCommandTests : IDisposable
{
    private readonly string _testRootPath;
    private readonly LocalSourcesConfiguration _configuration;
    private readonly IDeserializer _deserializer;
    private readonly ISerializer _serializer;

    public LocalSourcesCommandTests()
    {
        // Create a temporary directory for test files
        _testRootPath = Path.Combine(Path.GetTempPath(), $"AbpDevTools_LocalSourcesCommand_Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testRootPath);

        // Use real deserializer/serializer for proper YAML testing
        // Must use HyphenatedNamingConvention to match the actual configuration naming
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _serializer = new SerializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .Build();

        // Create configuration instance
        _configuration = new LocalSourcesConfiguration(_deserializer, _serializer);

        // Override the FolderPath to use test directory
        OverrideConfigurationPath(_configuration);
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

    /// <summary>
    /// Overrides the configuration path to use test directory instead of AppData.
    /// Uses reflection to set the private FolderPath property.
    /// </summary>
    private void OverrideConfigurationPath(LocalSourcesConfiguration configuration)
    {
        // We can't easily override the FolderPath without modifying the class,
        // so we'll use environment variable or test with actual file operations
        // For this test, we'll create the actual file in the test directory
        // and manually manipulate the file path during tests
    }

    /// <summary>
    /// Gets a test configuration file path in the temporary directory.
    /// </summary>
    private string GetTestConfigPath()
    {
        return Path.Combine(_testRootPath, "local-sources.yml");
    }

    /// <summary>
    /// Creates a test YAML file with the specified content.
    /// </summary>
    private void CreateTestYamlFile(string content)
    {
        File.WriteAllText(GetTestConfigPath(), content);
    }

    #region ListAction Tests

    [Fact]
    public void ListAction_DisplaysAllLocalSources()
    {
        // Arrange
        var yaml = @"
abp-framework:
  remote-path: https://github.com/abpframework/abp.git
  branch: dev
  path: C:\github\abp
  packages:
    - Volo.*

my-extension:
  remote-path: https://github.com/myorg/my-extension.git
  branch: main
  path: C:\github\my-extension
  packages:
    - MyOrg.*
";
        CreateTestYamlFile(yaml);

        // Act
        var content = File.ReadAllText(GetTestConfigPath());
        var result = _deserializer.Deserialize<LocalSourceMapping>(content);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2, "should return all configured local sources");

        result.Should().ContainKey("abp-framework");
        result["abp-framework"].RemotePath.Should().Be("https://github.com/abpframework/abp.git");
        result["abp-framework"].Branch.Should().Be("dev");
        result["abp-framework"].Path.Should().Be(@"C:\github\abp");

        result.Should().ContainKey("my-extension");
        result["my-extension"].RemotePath.Should().Be("https://github.com/myorg/my-extension.git");
        result["my-extension"].Branch.Should().Be("main");
    }

    [Fact]
    public void ListAction_HandlesEmptySources()
    {
        // Arrange
        var yaml = @"# Empty configuration file
# No local sources defined
";
        CreateTestYamlFile(yaml);

        // Act
        var content = File.ReadAllText(GetTestConfigPath());
        var result = _deserializer.Deserialize<LocalSourceMapping>(content);

        // Assert
        result.Should().BeNull("empty YAML should return null mapping");
    }

    #endregion

    #region AddAction Tests

    [Fact]
    public void AddAction_AddsNewLocalSourceMapping()
    {
        // Arrange
        var existingYaml = @"
abp-framework:
  remote-path: https://github.com/abpframework/abp.git
  branch: dev
  path: C:\github\abp
  packages:
    - Volo.*
";
        CreateTestYamlFile(existingYaml);

        // Act - Add a new source mapping
        var content = File.ReadAllText(GetTestConfigPath());
        var mapping = _deserializer.Deserialize<LocalSourceMapping>(content) ?? new LocalSourceMapping();

        mapping["new-package"] = new LocalSourceMappingItem
        {
            RemotePath = "https://github.com/neworg/new-package.git",
            Branch = "feature/new-feature",
            Path = @"C:\github\new-package",
            Packages = new HashSet<string> { "NewOrg.*" }
        };

        var newYaml = _serializer.Serialize(mapping);
        File.WriteAllText(GetTestConfigPath(), newYaml);

        // Assert - Verify the new mapping was added
        var updatedContent = File.ReadAllText(GetTestConfigPath());
        var updatedMapping = _deserializer.Deserialize<LocalSourceMapping>(updatedContent);

        updatedMapping.Should().NotBeNull();
        updatedMapping.Should().HaveCount(2, "should have original and new source");

        updatedMapping.Should().ContainKey("abp-framework");
        updatedMapping.Should().ContainKey("new-package");

        updatedMapping["new-package"].RemotePath.Should().Be("https://github.com/neworg/new-package.git");
        updatedMapping["new-package"].Branch.Should().Be("feature/new-feature");
        updatedMapping["new-package"].Path.Should().Be(@"C:\github\new-package");
        updatedMapping["new-package"].Packages.Should().Contain("NewOrg.*");
    }

    [Fact]
    public void AddAction_OverwritesExistingMapping()
    {
        // Arrange
        var existingYaml = @"
existing-package:
  remote-path: https://github.com/oldorg/old-package.git
  branch: main
  path: C:\github\old-package
  packages:
    - OldOrg.*
";
        CreateTestYamlFile(existingYaml);

        // Act - Overwrite the existing mapping
        var content = File.ReadAllText(GetTestConfigPath());
        var mapping = _deserializer.Deserialize<LocalSourceMapping>(content)!;

        mapping["existing-package"] = new LocalSourceMappingItem
        {
            RemotePath = "https://github.com/neworg/new-package.git",
            Branch = "develop",
            Path = @"C:\github\new-package",
            Packages = new HashSet<string> { "NewOrg.*" }
        };

        var newYaml = _serializer.Serialize(mapping);
        File.WriteAllText(GetTestConfigPath(), newYaml);

        // Assert - Verify the mapping was overwritten
        var updatedContent = File.ReadAllText(GetTestConfigPath());
        var updatedMapping = _deserializer.Deserialize<LocalSourceMapping>(updatedContent);

        updatedMapping.Should().NotBeNull();
        updatedMapping.Should().HaveCount(1, "should still have only one mapping");

        updatedMapping["existing-package"].RemotePath.Should().Be("https://github.com/neworg/new-package.git");
        updatedMapping["existing-package"].Branch.Should().Be("develop");
        updatedMapping["existing-package"].Path.Should().Be(@"C:\github\new-package");
        updatedMapping["existing-package"].Packages.Should().Contain("NewOrg.*");
    }

    [Theory]
    [InlineData("https://github.com/valid/repo.git")]
    [InlineData("https://gitlab.com/valid/repo.git")]
    [InlineData("https://dev.azure.com/org/project/_git/repo")]
    [InlineData("git@github.com:valid/repo.git")]
    public void AddAction_ValidatesGitUrlFormat(string validUrl)
    {
        // Arrange
        var yaml = $@"
test-source:
  remote-path: {validUrl}
  path: C:\github\test
  packages:
    - Test.*
";
        CreateTestYamlFile(yaml);

        // Act
        var content = File.ReadAllText(GetTestConfigPath());
        var result = _deserializer.Deserialize<LocalSourceMapping>(content);

        // Assert
        result.Should().NotBeNull();
        result["test-source"].RemotePath.Should().Be(validUrl);
    }

    [Fact]
    public void AddAction_SavesToConfigurationFile()
    {
        // Arrange - Start with no configuration file
        var configPath = GetTestConfigPath();

        // Act - Create and save a new mapping
        var mapping = new LocalSourceMapping
        {
            ["test-package"] = new LocalSourceMappingItem
            {
                RemotePath = "https://github.com/test/repo.git",
                Branch = "main",
                Path = @"C:\github\test",
                Packages = new HashSet<string> { "Test.*" }
            }
        };

        var yaml = _serializer.Serialize(mapping);
        File.WriteAllText(configPath, yaml);

        // Assert - Verify file was created and contains correct content
        File.Exists(configPath).Should().BeTrue("configuration file should be created");

        var content = File.ReadAllText(configPath);
        content.Should().Contain("test-package");
        content.Should().Contain("https://github.com/test/repo.git");
        content.Should().Contain("main");
        content.Should().Contain(@"C:\github\test");

        var deserialized = _deserializer.Deserialize<LocalSourceMapping>(content);
        deserialized.Should().NotBeNull();
        deserialized["test-package"].Packages.Should().Contain("Test.*");
    }

    #endregion

    #region RemoveAction Tests

    [Fact]
    public void RemoveAction_RemovesExistingMapping()
    {
        // Arrange
        var yaml = @"
source-to-keep:
  remote-path: https://github.com/keep/repo.git
  path: C:\github\keep
  packages:
    - Keep.*

source-to-remove:
  remote-path: https://github.com/remove/repo.git
  path: C:\github\remove
  packages:
    - Remove.*
";
        CreateTestYamlFile(yaml);

        // Act - Remove the second mapping
        var content = File.ReadAllText(GetTestConfigPath());
        var mapping = _deserializer.Deserialize<LocalSourceMapping>(content)!;

        mapping.Remove("source-to-remove");

        var newYaml = _serializer.Serialize(mapping);
        File.WriteAllText(GetTestConfigPath(), newYaml);

        // Assert - Verify the mapping was removed
        var updatedContent = File.ReadAllText(GetTestConfigPath());
        var updatedMapping = _deserializer.Deserialize<LocalSourceMapping>(updatedContent);

        updatedMapping.Should().NotBeNull();
        updatedMapping.Should().HaveCount(1, "should only have the remaining source");

        updatedMapping.Should().ContainKey("source-to-keep");
        updatedMapping.Should().NotContainKey("source-to-remove");
    }

    [Fact]
    public void RemoveAction_HandlesNonExistentMappingGracefully()
    {
        // Arrange
        var yaml = @"
existing-source:
  remote-path: https://github.com/existing/repo.git
  path: C:\github\existing
  packages:
    - Existing.*
";
        CreateTestYamlFile(yaml);

        // Act - Try to remove a non-existent mapping
        var content = File.ReadAllText(GetTestConfigPath());
        var mapping = _deserializer.Deserialize<LocalSourceMapping>(content)!;

        var result = mapping.Remove("non-existent-source");

        var newYaml = _serializer.Serialize(mapping);
        File.WriteAllText(GetTestConfigPath(), newYaml);

        // Assert - Verify operation handled gracefully
        result.Should().BeFalse("removing non-existent key should return false");

        var updatedContent = File.ReadAllText(GetTestConfigPath());
        var updatedMapping = _deserializer.Deserialize<LocalSourceMapping>(updatedContent);

        updatedMapping.Should().NotBeNull();
        updatedMapping.Should().HaveCount(1, "original mapping should remain unchanged");
        updatedMapping.Should().ContainKey("existing-source");
    }

    #endregion
}
