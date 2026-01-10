using AbpDevTools.Configuration;
using AbpDevTools.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace AbpDevTools.Tests.Configuration;

/// <summary>
/// Unit tests for <see cref="LocalSourcesConfiguration"/> class.
/// Tests YAML deserialization, validation, and edge cases for local source mappings.
/// </summary>
public class LocalSourcesConfigurationTests : ConfigurationTestBase
{
    #region Valid YAML Deserialization Tests

    [Fact]
    public void Deserialize_ValidYaml_WithSourceMappings_ShouldSucceed()
    {
        // Arrange
        var yaml = @"
abp:
  remote-path: https://github.com/abpframework/abp.git
  branch: dev
  path: C:\github\abp
  packages:
    - Volo.*
";

        // Act
        var result = DeserializeYaml<LocalSourceMapping>(yaml);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.Should().ContainKey("abp");

        var abpMapping = result["abp"];
        abpMapping.RemotePath.Should().Be("https://github.com/abpframework/abp.git");
        abpMapping.Branch.Should().Be("dev");
        abpMapping.Path.Should().Be(@"C:\github\abp");
        abpMapping.Packages.Should().HaveCount(1);
        abpMapping.Packages.Should().Contain("Volo.*");
    }

    [Fact]
    public void Deserialize_ValidYaml_WithBranchSpecification_ShouldIncludeBranch()
    {
        // Arrange
        var yaml = @"
my-package:
  remote-path: https://github.com/myorg/my-package.git
  branch: feature/new-feature
  path: C:\github\my-package
  packages:
    - MyOrg.*
";

        // Act
        var result = DeserializeYaml<LocalSourceMapping>(yaml);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainKey("my-package");

        var mapping = result["my-package"];
        mapping.Branch.Should().Be("feature/new-feature");
        mapping.RemotePath.Should().Be("https://github.com/myorg/my-package.git");
        mapping.Path.Should().Be(@"C:\github\my-package");
    }

    [Theory]
    [InlineData("main")]
    [InlineData("develop")]
    [InlineData("feature/awesome-feature")]
    [InlineData("release/v1.0.0")]
    [InlineData("hotfix/bug-123")]
    public void Deserialize_ValidYaml_WithVariousBranchNames_ShouldParseCorrectly(string branchName)
    {
        // Arrange
        var yaml = $@"
test-source:
  remote-path: https://github.com/test/repo.git
  branch: {branchName}
  path: C:\github\repo
  packages:
    - Test.*
";

        // Act
        var result = DeserializeYaml<LocalSourceMapping>(yaml);

        // Assert
        result.Should().NotBeNull();
        result["test-source"].Branch.Should().Be(branchName);
    }

    #endregion

    #region Empty and Minimal Configuration Tests

    [Fact]
    public void Deserialize_EmptyMappings_ShouldReturnNull()
    {
        // Arrange
        var yaml = @"# Empty configuration file
# No local sources defined
";

        // Act
        var result = DeserializeYaml<LocalSourceMapping>(yaml);

        // Assert
        // YAML deserialization returns null for empty documents
        result.Should().BeNull();
    }

    [Fact]
    public void Deserialize_YamlWithNoBranch_ShouldUseDefaults()
    {
        // Arrange
        var yaml = @"
simple-package:
  path: C:\github\simple-package
  packages:
    - Simple.*
";

        // Act
        var result = DeserializeYaml<LocalSourceMapping>(yaml);

        // Assert
        result.Should().NotBeNull();
        result["simple-package"].Branch.Should().BeNull();
        result["simple-package"].RemotePath.Should().BeNull();
        result["simple-package"].Path.Should().Be(@"C:\github\simple-package");
        result["simple-package"].Packages.Should().Contain("Simple.*");
    }

    #endregion

    #region Duplicate Package Name Tests

    [Fact]
    public void Deserialize_DuplicatePackageNames_LastOneShouldWin()
    {
        // Arrange
        var yaml = @"
duplicate-source:
  path: C:\github\first-version
  packages:
    - First.*

duplicate-source:
  path: C:\github\second-version
  packages:
    - Second.*
";

        // Act
        var result = DeserializeYaml<LocalSourceMapping>(yaml);

        // Assert
        result.Should().HaveCount(1);
        result.Should().ContainKey("duplicate-source");

        // YAML deserializer's last occurrence should win
        var mapping = result["duplicate-source"];
        mapping.Path.Should().Be(@"C:\github\second-version");
        mapping.Packages.Should().HaveCount(1);
        mapping.Packages.Should().Contain("Second.*");
    }

    #endregion

    #region Invalid Git URL Format Tests

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("github.com/repo")]
    [InlineData("ftp://invalid-protocol.com")]
    [InlineData("C:\\local\\path\\not\\url")]
    [InlineData("/absolute/path/not/url")]
    public void Deserialize_InvalidGitUrlFormat_ShouldStillDeserialize(string invalidUrl)
    {
        // Arrange
        var yaml = $@"
test-source:
  remote-path: {invalidUrl}
  path: C:\github\test
  packages:
    - Test.*
";

        // Act
        var result = DeserializeYaml<LocalSourceMapping>(yaml);

        // Assert
        // YAML deserialization doesn't validate URL format
        // The actual validation would happen during runtime usage
        result.Should().NotBeNull();
        result.Should().ContainKey("test-source");
        result["test-source"].RemotePath.Should().Be(invalidUrl);
    }

    [Fact]
    public void Deserialize_EmptyRemotePath_ShouldDeserializeAsNull()
    {
        // Arrange
        var yaml = @"
test-source:
  remote-path:
  path: C:\github\test
  packages:
    - Test.*
";

        // Act
        var result = DeserializeYaml<LocalSourceMapping>(yaml);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainKey("test-source");
        // Empty YAML values deserialize to null
        result["test-source"].RemotePath.Should().BeNull();
    }

    [Theory]
    [InlineData("https://github.com/valid/repo.git")]
    [InlineData("https://gitlab.com/valid/repo.git")]
    [InlineData("https://dev.azure.com/org/project/_git/repo")]
    [InlineData("git@github.com:valid/repo.git")]
    public void Deserialize_ValidGitUrlFormats_ShouldDeserializeCorrectly(string validUrl)
    {
        // Arrange
        var yaml = $@"
valid-source:
  remote-path: {validUrl}
  path: C:\github\valid
  packages:
    - Valid.*
";

        // Act
        var result = DeserializeYaml<LocalSourceMapping>(yaml);

        // Assert
        result.Should().NotBeNull();
        result["valid-source"].RemotePath.Should().Be(validUrl);
    }

    #endregion

    #region Multiple Sources Tests

    [Fact]
    public void Deserialize_MultipleSourceMappings_ShouldDeserializeAll()
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

third-party:
  remote-path: https://github.com/thirdparty/lib.git
  path: C:\github\third-party
  packages:
    - ThirdParty.*
";

        // Act
        var result = DeserializeYaml<LocalSourceMapping>(yaml);

        // Assert
        result.Should().HaveCount(3);

        result.Should().ContainKey("abp-framework");
        result.Should().ContainKey("my-extension");
        result.Should().ContainKey("third-party");

        result["abp-framework"].Branch.Should().Be("dev");
        result["my-extension"].Branch.Should().Be("main");
        result["third-party"].Branch.Should().BeNull();
    }

    #endregion

    #region Multiple Packages in Single Source Tests

    [Fact]
    public void Deserialize_MultiplePackagesInSingleSource_ShouldDeserializeAll()
    {
        // Arrange
        var yaml = @"
comprehensive-source:
  remote-path: https://github.com/comprehensive/repo.git
  branch: main
  path: C:\github\comprehensive
  packages:
    - PackageA.*
    - PackageB.*
    - PackageC.*
    - SinglePackage
    - Another.*
";

        // Act
        var result = DeserializeYaml<LocalSourceMapping>(yaml);

        // Assert
        result.Should().HaveCount(1);
        result["comprehensive-source"].Packages.Should().HaveCount(5);

        result["comprehensive-source"].Packages.Should().Contain("PackageA.*");
        result["comprehensive-source"].Packages.Should().Contain("PackageB.*");
        result["comprehensive-source"].Packages.Should().Contain("PackageC.*");
        result["comprehensive-source"].Packages.Should().Contain("SinglePackage");
        result["comprehensive-source"].Packages.Should().Contain("Another.*");
    }

    #endregion

    #region Path Handling Tests

    [Theory]
    [InlineData(@"C:\github\repo")]
    [InlineData(@"/home/user/github/repo")]
    [InlineData("~/github/repo")]
    [InlineData(@"..\..\relative\path")]
    public void Deserialize_DifferentPathFormats_ShouldPreserveAsIs(string pathValue)
    {
        // Arrange
        var yaml = $@"
path-test:
  path: {pathValue}
  packages:
    - PathTest.*
";

        // Act
        var result = DeserializeYaml<LocalSourceMapping>(yaml);

        // Assert
        result.Should().NotBeNull();
        result["path-test"].Path.Should().Be(pathValue);
    }

    #endregion
}
