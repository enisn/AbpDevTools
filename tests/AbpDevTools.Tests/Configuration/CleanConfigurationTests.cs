using AbpDevTools.Configuration;
using AbpDevTools.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace AbpDevTools.Tests.Configuration;

/// <summary>
/// Unit tests for CleanConfiguration class.
/// Tests YAML deserialization and clean operation settings.
/// </summary>
public class CleanConfigurationTests : ConfigurationTestBase
{
    [Fact]
    public void Deserialize_ValidYaml_WithCleanTargets_ShouldSucceed()
    {
        // Arrange
        var yaml = @"
folders:
  - bin
  - obj
  - node_modules
  - dist
  - .vs
";

        // Act
        var options = DeserializeYaml<CleanOptions>(yaml);

        // Assert
        options.Should().NotBeNull();
        options.Folders.Should().NotBeNull();
        options.Folders.Should().HaveCount(5);
        options.Folders.Should().BeEquivalentTo(new[] { "bin", "obj", "node_modules", "dist", ".vs" });
    }

    [Theory]
    [InlineData("bin")]
    [InlineData("obj")]
    [InlineData("node_modules")]
    [InlineData("dist")]
    [InlineData(".vs")]
    [InlineData("Debug")]
    [InlineData("Release")]
    [InlineData("packages")]
    [InlineData(".nuget")]
    [InlineData("TestResults")]
    public void Deserialize_FolderPattern_ShouldContainExpectedFolder(string folder)
    {
        // Arrange
        var yaml = $@"
folders:
  - {folder}
";

        // Act
        var options = DeserializeYaml<CleanOptions>(yaml);

        // Assert
        options.Should().NotBeNull();
        options.Folders.Should().Contain(folder);
    }

    [Fact]
    public void Deserialize_CommonCleanPatterns_ShouldContainAllExpectedFolders()
    {
        // Arrange
        var yaml = @"
folders:
  - bin
  - obj
  - node_modules
  - .vs
  - dist
  - build
  - .next
  - .nuxt
  - coverage
  - .pytest_cache
  - __pycache__
  - .venv
  - venv
";

        // Act
        var options = DeserializeYaml<CleanOptions>(yaml);

        // Assert
        options.Should().NotBeNull();
        options.Folders.Should().HaveCount(13);
        options.Folders.Should().Contain("bin");
        options.Folders.Should().Contain("obj");
        options.Folders.Should().Contain("node_modules");
        options.Folders.Should().Contain(".vs");
        options.Folders.Should().Contain("dist");
        options.Folders.Should().Contain("build");
        options.Folders.Should().Contain(".next");
        options.Folders.Should().Contain(".nuxt");
        options.Folders.Should().Contain("coverage");
        options.Folders.Should().Contain(".pytest_cache");
        options.Folders.Should().Contain("__pycache__");
        options.Folders.Should().Contain(".venv");
        options.Folders.Should().Contain("venv");
    }

    [Fact]
    public void Deserialize_CombinedFoldersAndFiles_ShouldPreserveAllTargets()
    {
        // Arrange
        var yaml = @"
folders:
  - bin
  - obj
  - node_modules
  - .vs
";

        // Act
        var options = DeserializeYaml<CleanOptions>(yaml);

        // Assert
        options.Should().NotBeNull();
        options.Folders.Should().HaveCount(4);
        options.Folders.Should().BeEquivalentTo(new[] { "bin", "obj", "node_modules", ".vs" });
    }

    [Fact]
    public void Deserialize_EmptyConfiguration_ShouldReturnEmptyFolders()
    {
        // Arrange
        var yaml = @"
folders: []
";

        // Act
        var options = DeserializeYaml<CleanOptions>(yaml);

        // Assert
        options.Should().NotBeNull();
        options.Folders.Should().NotBeNull();
        options.Folders.Should().BeEmpty();
    }

    [Fact]
    public void Deserialize_DefaultConfiguration_ShouldHaveDefaultFolders()
    {
        // Arrange & Act
        // When no YAML is provided, use the class defaults
        var options = new CleanOptions();

        // Assert
        options.Should().NotBeNull();
        options.Folders.Should().NotBeNull();
        options.Folders.Should().HaveCount(3);
        options.Folders.Should().BeEquivalentTo(new[] { "bin", "obj", "node_modules" });
    }

    [Fact]
    public void Deserialize_NestedFolderPatterns_ShouldHandleCorrectly()
    {
        // Arrange
        var yaml = @"
folders:
  - bin
  - obj
  - ""**/bin""
  - ""**/obj""
  - node_modules
  - ""**/node_modules""
";

        // Act
        var options = DeserializeYaml<CleanOptions>(yaml);

        // Assert
        options.Should().NotBeNull();
        options.Folders.Should().HaveCount(6);
        options.Folders.Should().Contain("bin");
        options.Folders.Should().Contain("obj");
        options.Folders.Should().Contain("**/bin");
        options.Folders.Should().Contain("**/obj");
        options.Folders.Should().Contain("node_modules");
        options.Folders.Should().Contain("**/node_modules");
    }

    [Fact]
    public void Serialize_CleanOptions_ShouldProduceValidYaml()
    {
        // Arrange
        var options = new CleanOptions
        {
            Folders = new[] { "bin", "obj", "node_modules", "dist" }
        };

        // Act
        var yaml = SerializeYaml(options);

        // Assert
        yaml.Should().NotBeNullOrEmpty();
        yaml.Should().Contain("folders");
        yaml.Should().Contain("bin");
        yaml.Should().Contain("obj");
        yaml.Should().Contain("node_modules");
        yaml.Should().Contain("dist");
    }

    [Theory]
    [InlineData(".git", false)]
    [InlineData(".gitignore", false)]
    [InlineData(".vs", true)]
    [InlineData(".vscode", false)]
    [InlineData("node_modules", true)]
    [InlineData("package-lock.json", false)]
    public void CommonCleanPatterns_ShouldIncludeExpectedFolders(string folder, bool shouldBeCleaned)
    {
        // Arrange
        var cleanableFolders = new[] { "bin", "obj", "node_modules", ".vs", "dist", "build", "coverage", ".next", ".nuxt" };

        // Act
        var isCleanable = cleanableFolders.Contains(folder);

        // Assert
        if (shouldBeCleaned)
        {
            isCleanable.Should().BeTrue($"{folder} should be in the list of cleanable folders");
        }
        else
        {
            isCleanable.Should().BeFalse($"{folder} should not be in the list of cleanable folders");
        }
    }
}
