using System.Xml.Linq;
using AbpDevTools.Configuration;
using AbpDevTools.Services;
using AbpDevTools.Tests.Helpers;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AbpDevTools.Tests.Services;

/// <summary>
/// Unit tests for CsprojManipulationService PackageReference to ProjectReference conversion.
/// Tests conversion logic, version backup, and XML formatting preservation.
/// </summary>
public class CsprojManipulationService_PackageToProjectTests
{
    private readonly CsprojManipulationService _service;
    private readonly FileExplorer _mockFileExplorer;

    public CsprojManipulationService_PackageToProjectTests()
    {
        _mockFileExplorer = Substitute.For<FileExplorer>();
        _service = new CsprojManipulationService(_mockFileExplorer);
    }

    [Fact]
    public void ConvertToProjectReference_SinglePackageReference_ShouldConvertCorrectly()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Volo.Abp.Core"" Version=""5.3.0"" />
  </ItemGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);
        var packageRef = doc.Descendants("PackageReference").First();
        var projectPath = @"..\..\modules\abp\core\Volo.Abp.Core\Volo.Abp.Core.csproj";

        // Act
        _service.ConvertToProjectReference(packageRef, projectPath);

        // Assert
        packageRef.Name.LocalName.Should().Be("ProjectReference");
        packageRef.Attribute("Include")?.Value.Should().Be(projectPath);
        packageRef.Attribute("Version").Should().BeNull();
    }

    [Fact]
    public void ConvertToProjectReference_MultiplePackageReferences_ShouldConvertAll()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Volo.Abp.Core"" Version=""5.3.0"" />
    <PackageReference Include=""Volo.Abp.Data"" Version=""5.3.0"" />
    <PackageReference Include=""Volo.Abp.EntityFrameworkCore"" Version=""5.3.0"" />
  </ItemGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);
        var packageRefs = doc.Descendants("PackageReference").ToList();
        var projectPaths = new[]
        {
            @"..\..\modules\abp\core\Volo.Abp.Core\Volo.Abp.Core.csproj",
            @"..\..\modules\abp\data\Volo.Abp.Data\Volo.Abp.Data.csproj",
            @"..\..\modules\abp\entityframeworkcore\Volo.Abp.EntityFrameworkCore\Volo.Abp.EntityFrameworkCore.csproj"
        };

        // Act
        for (int i = 0; i < packageRefs.Count; i++)
        {
            _service.ConvertToProjectReference(packageRefs[i], projectPaths[i]);
        }

        // Assert
        var projectRefs = doc.Descendants("ProjectReference").ToList();
        projectRefs.Should().HaveCount(3);
        projectRefs[0].Attribute("Include")?.Value.Should().Be(projectPaths[0]);
        projectRefs[1].Attribute("Include")?.Value.Should().Be(projectPaths[1]);
        projectRefs[2].Attribute("Include")?.Value.Should().Be(projectPaths[2]);
    }

    [Fact]
    public void AddVersionBackupProperties_SingleVersion_ShouldStoreCorrectly()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);
        var versionsToBackup = new Dictionary<string, string>
        {
            { "Volo.Abp.Core", "5.3.0" }
        };

        // Act
        _service.AddVersionBackupProperties(doc, versionsToBackup);

        // Assert
        var versionProperty = doc.Descendants("PropertyGroup")
            .SelectMany(pg => pg.Elements("Volo.Abp.CoreVersion"))
            .FirstOrDefault();
        versionProperty.Should().NotBeNull();
        versionProperty?.Value.Should().Be("5.3.0");
    }

    [Fact]
    public void AddVersionBackupProperties_MultipleVersions_ShouldStoreAll()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);
        var versionsToBackup = new Dictionary<string, string>
        {
            { "Volo.Abp.Core", "5.3.0" },
            { "Volo.Abp.Data", "5.3.0" },
            { "Volo.Abp.EntityFrameworkCore", "5.3.1" }
        };

        // Act
        _service.AddVersionBackupProperties(doc, versionsToBackup);

        // Assert
        var propertyGroup = doc.Descendants("PropertyGroup").First();
        propertyGroup.Element("Volo.Abp.CoreVersion")?.Value.Should().Be("5.3.0");
        propertyGroup.Element("Volo.Abp.DataVersion")?.Value.Should().Be("5.3.0");
        propertyGroup.Element("Volo.Abp.EntityFrameworkCoreVersion")?.Value.Should().Be("5.3.1");
    }

    [Fact]
    public void AddVersionBackupProperties_ExistingPropertyGroupWithVersions_ShouldReuseGroup()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <SomeOtherVersion>1.0.0</SomeOtherVersion>
  </PropertyGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);
        var versionsToBackup = new Dictionary<string, string>
        {
            { "Volo.Abp.Core", "5.3.0" }
        };

        // Act
        _service.AddVersionBackupProperties(doc, versionsToBackup);

        // Assert
        var propertyGroups = doc.Descendants("PropertyGroup").ToList();
        propertyGroups.Should().HaveCount(1, "Should reuse existing PropertyGroup");
        propertyGroups[0].Element("Volo.Abp.CoreVersion")?.Value.Should().Be("5.3.0");
        propertyGroups[0].Element("SomeOtherVersion")?.Value.Should().Be("1.0.0", "Should preserve existing properties");
    }

    [Fact]
    public void ConvertToProjectReference_PackageReferenceWithNoVersion_ShouldRemoveVersionAttribute()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Volo.Abp.Core"" />
  </ItemGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);
        var packageRef = doc.Descendants("PackageReference").First();
        var projectPath = @"..\..\modules\abp\core\Volo.Abp.Core\Volo.Abp.Core.csproj";

        // Act
        _service.ConvertToProjectReference(packageRef, projectPath);

        // Assert
        packageRef.Name.LocalName.Should().Be("ProjectReference");
        packageRef.Attribute("Include")?.Value.Should().Be(projectPath);
        packageRef.Attribute("Version").Should().BeNull();
    }

    [Fact]
    public void GetMatchingPackageReferences_CaseInsensitive_ShouldMatchRegardlessOfCase()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Volo.Abp.Core"" Version=""5.3.0"" />
    <PackageReference Include=""volo.abp.data"" Version=""5.3.0"" />
    <PackageReference Include=""VOLO.ABP.ENTITYFRAMEWORKCORE"" Version=""5.3.0"" />
  </ItemGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);

        // Act & Assert
        var matchesLower = _service.GetMatchingPackageReferences(doc, "volo.abp.core");
        matchesLower.Should().HaveCount(1);

        var matchesUpper = _service.GetMatchingPackageReferences(doc, "VOLO.ABP.DATA");
        matchesUpper.Should().HaveCount(1);

        var matchesMixed = _service.GetMatchingPackageReferences(doc, "Volo.Abp.EntityFrameworkCore");
        matchesMixed.Should().HaveCount(1);
    }

    [Fact]
    public void GetMatchingPackageReferences_WildcardPattern_ShouldMatchPrefix()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Volo.Abp.Core"" Version=""5.3.0"" />
    <PackageReference Include=""Volo.Abp.Data"" Version=""5.3.0"" />
    <PackageReference Include=""Volo.Abp.EntityFrameworkCore"" Version=""5.3.0"" />
    <PackageReference Include=""Microsoft.Extensions.Logging"" Version=""8.0.0"" />
  </ItemGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);

        // Act
        var matches = _service.GetMatchingPackageReferences(doc, "Volo.Abp.*");

        // Assert
        matches.Should().HaveCount(3, "Should match all packages starting with 'Volo.Abp.'");
        matches.All(pr => pr.Attribute("Include")?.Value.StartsWith("Volo.Abp.", StringComparison.OrdinalIgnoreCase) == true)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("PrivateAssets", "all")]
    [InlineData("IncludeAssets", "compile")]
    [InlineData("ExcludeAssets", "runtime")]
    public void ConvertToProjectReference_ShouldRemovePackageSpecificAttributes(string attributeName, string attributeValue)
    {
        // Arrange
        var csprojXml = $@"
<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Volo.Abp.Core"" Version=""5.3.0"" {attributeName}=""{attributeValue}"" />
  </ItemGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);
        var packageRef = doc.Descendants("PackageReference").First();
        var projectPath = @"..\..\modules\abp\core\Volo.Abp.Core\Volo.Abp.Core.csproj";

        // Act
        _service.ConvertToProjectReference(packageRef, projectPath);

        // Assert
        packageRef.Name.LocalName.Should().Be("ProjectReference");
        packageRef.Attribute("Include")?.Value.Should().Be(projectPath);
        packageRef.Attribute(attributeName).Should().BeNull($"Should remove {attributeName} attribute");
    }

    [Fact]
    public void GetBackedUpVersion_ExistingVersionProperty_ShouldReturnVersion()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Volo.Abp.CoreVersion>5.3.0</Volo.Abp.CoreVersion>
  </PropertyGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);

        // Act
        var version = _service.GetBackedUpVersion(doc, "Volo.Abp.Core");

        // Assert
        version.Should().Be("5.3.0");
    }

    [Fact]
    public void GetBackedUpVersion_NoVersionProperty_ShouldReturnNull()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);

        // Act
        var version = _service.GetBackedUpVersion(doc, "Volo.Abp.Core");

        // Assert
        version.Should().BeNull();
    }

    [Fact]
    public void AddVersionBackupProperties_EmptyVersionDictionary_ShouldNotModifyDocument()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);
        var originalXml = doc.ToString();
        var versionsToBackup = new Dictionary<string, string>();

        // Act
        _service.AddVersionBackupProperties(doc, versionsToBackup);

        // Assert
        doc.ToString().Should().Be(originalXml, "Document should not be modified for empty version dictionary");
    }

    [Fact]
    public void ConvertToProjectReference_PreservesXmlFormatting()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Volo.Abp.Core"" Version=""5.3.0"" />
  </ItemGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);
        var packageRef = doc.Descendants("PackageReference").First();
        var projectPath = @"..\..\modules\abp\core\Volo.Abp.Core\Volo.Abp.Core.csproj";

        // Act
        _service.ConvertToProjectReference(packageRef, projectPath);

        // Assert
        var projectRef = doc.Descendants("ProjectReference").First();
        projectRef.Should().NotBeNull();
        projectRef.Name.LocalName.Should().Be("ProjectReference");
        projectRef.Attribute("Include")?.Value.Should().Be(projectPath);

        // Verify no additional unwanted elements or attributes were added
        projectRef.Attributes().Count().Should().Be(1, "Should only have Include attribute");
    }

    [Fact]
    public void BuildProjectLookupCache_NonExistentDirectory_ShouldReturnEmptyDictionary()
    {
        // Arrange
        var sources = new List<KeyValuePair<string, LocalSourceMappingItem>>
        {
            KeyValuePair.Create("nonexistent", new LocalSourceMappingItem { Path = @"C:\nonexistent\path\that\does\not\exist" })
        };

        // Act
        var cache = _service.BuildProjectLookupCache(sources);

        // Assert
        cache.Should().HaveCount(1);
        cache["nonexistent"].Should().BeEmpty("Non-existent directory should return empty project dictionary");
    }

    [Fact]
    public void FindLocalProject_ExistingProject_ShouldReturnProjectPath()
    {
        // Arrange
        var projectLookupCache = new Dictionary<string, Dictionary<string, string>>
        {
            ["abp"] = new Dictionary<string, string>
            {
                ["Volo.Abp.Core"] = @"C:\dev\abp\core\Volo.Abp.Core\Volo.Abp.Core.csproj"
            }
        };

        // Act
        var result = _service.FindLocalProject("Volo.Abp.Core", "abp", projectLookupCache);

        // Assert
        result.Should().Be(@"C:\dev\abp\core\Volo.Abp.Core\Volo.Abp.Core.csproj");
    }

    [Fact]
    public void FindLocalProject_NonExistingProject_ShouldReturnNull()
    {
        // Arrange
        var projectLookupCache = new Dictionary<string, Dictionary<string, string>>
        {
            ["abp"] = new Dictionary<string, string>()
        };

        // Act
        var result = _service.FindLocalProject("Volo.Abp.Core", "abp", projectLookupCache);

        // Assert
        result.Should().BeNull();
    }
}
