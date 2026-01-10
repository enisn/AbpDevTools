using System.Xml.Linq;
using AbpDevTools.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AbpDevTools.Tests.Services;

/// <summary>
/// Unit tests for CsprojManipulationService ProjectReference to PackageReference conversion.
/// Tests reverse conversion and version restoration from backup metadata.
/// </summary>
public class CsprojManipulationService_ProjectToPackageTests
{
    private readonly CsprojManipulationService _service;
    private readonly FileExplorer _mockFileExplorer;

    public CsprojManipulationService_ProjectToPackageTests()
    {
        _mockFileExplorer = Substitute.For<FileExplorer>();
        _service = new CsprojManipulationService(_mockFileExplorer);
    }

    [Fact]
    public void ConvertToPackageReference_SingleProjectReference_ShouldReplaceWithPackageReference()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <ProjectReference Include=""..\..\src\MyProject\MyProject.csproj"" />
  </ItemGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);
        var projectRef = doc.Descendants("ProjectReference").First();

        // Act
        _service.ConvertToPackageReference(projectRef, "MyProject", "1.2.3");

        // Assert
        projectRef.Name.LocalName.Should().Be("PackageReference");
        projectRef.Attribute("Include")?.Value.Should().Be("MyProject");
        projectRef.Attribute("Version")?.Value.Should().Be("1.2.3");
    }

    [Fact]
    public void ConvertToPackageReference_MultipleProjectReferences_ShouldReplaceAll()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <ProjectReference Include=""..\..\src\ProjectA\ProjectA.csproj"" />
    <ProjectReference Include=""..\..\src\ProjectB\ProjectB.csproj"" />
    <ProjectReference Include=""..\..\src\ProjectC\ProjectC.csproj"" />
  </ItemGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);
        var projectRefs = doc.Descendants("ProjectReference").ToList();

        // Act
        _service.ConvertToPackageReference(projectRefs[0], "ProjectA", "1.0.0");
        _service.ConvertToPackageReference(projectRefs[1], "ProjectB", "2.0.0");
        _service.ConvertToPackageReference(projectRefs[2], "ProjectC", "3.0.0");

        // Assert
        var packageRefs = doc.Descendants("PackageReference").ToList();
        packageRefs.Should().HaveCount(3);

        packageRefs[0].Attribute("Include")?.Value.Should().Be("ProjectA");
        packageRefs[0].Attribute("Version")?.Value.Should().Be("1.0.0");

        packageRefs[1].Attribute("Include")?.Value.Should().Be("ProjectB");
        packageRefs[1].Attribute("Version")?.Value.Should().Be("2.0.0");

        packageRefs[2].Attribute("Include")?.Value.Should().Be("ProjectC");
        packageRefs[2].Attribute("Version")?.Value.Should().Be("3.0.0");
    }

    [Fact]
    public void GetBackedUpVersion_WithBackupProperty_ShouldRestoreOriginalVersion()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <VoloAbpVersion>8.2.0</VoloAbpVersion>
  </PropertyGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);

        // Act
        var version = _service.GetBackedUpVersion(doc, "VoloAbp");

        // Assert
        version.Should().Be("8.2.0");
    }

    [Fact]
    public void ConvertToPackageReference_WithVersionBackup_ShouldUseBackedUpVersion()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <MyLibVersion>5.4.3</MyLibVersion>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include=""..\..\src\MyLib\MyLib.csproj"" />
  </ItemGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);
        var projectRef = doc.Descendants("ProjectReference").First();
        var backupVersion = _service.GetBackedUpVersion(doc, "MyLib");

        // Act
        _service.ConvertToPackageReference(projectRef, "MyLib", backupVersion ?? "latest");

        // Assert
        projectRef.Name.LocalName.Should().Be("PackageReference");
        projectRef.Attribute("Include")?.Value.Should().Be("MyLib");
        projectRef.Attribute("Version")?.Value.Should().Be("5.4.3");
    }

    [Fact]
    public void ConvertToPackageReference_ProjectNotInMapping_ShouldKeepProjectReference()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <ProjectReference Include=""..\..\src\KnownProject\KnownProject.csproj"" />
    <ProjectReference Include=""..\..\src\UnknownProject\UnknownProject.csproj"" />
  </ItemGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);
        var projectRefs = doc.Descendants("ProjectReference").ToList();

        // Act - Convert only the known project
        _service.ConvertToPackageReference(projectRefs[0], "KnownProject", "1.0.0");

        // Assert
        var packageRefs = doc.Descendants("PackageReference").ToList();
        var remainingProjectRefs = doc.Descendants("ProjectReference").ToList();

        packageRefs.Should().HaveCount(1);
        packageRefs[0].Attribute("Include")?.Value.Should().Be("KnownProject");

        remainingProjectRefs.Should().HaveCount(1);
        remainingProjectRefs[0].Attribute("Include")?.Value.Should().Be(@"..\..\src\UnknownProject\UnknownProject.csproj");
    }

    [Fact]
    public void ConvertToPackageReference_MissingBackupVersion_ShouldUseWildcard()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include=""..\..\src\MyLib\MyLib.csproj"" />
  </ItemGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);
        var projectRef = doc.Descendants("ProjectReference").First();
        var backupVersion = _service.GetBackedUpVersion(doc, "MyLib");

        // Act
        _service.ConvertToPackageReference(projectRef, "MyLib", backupVersion ?? "*");

        // Assert
        projectRef.Name.LocalName.Should().Be("PackageReference");
        projectRef.Attribute("Include")?.Value.Should().Be("MyLib");
        projectRef.Attribute("Version")?.Value.Should().Be("*");
    }

    [Fact]
    public void ConvertToPackageReference_PreservesXmlFormatting()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""FluentAssertions"" Version=""8.8.0"" />
    <ProjectReference Include=""..\..\src\MyProject\MyProject.csproj"" />
    <PackageReference Include=""xunit"" Version=""2.6.11"" />
  </ItemGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);
        var itemGroup = doc.Descendants("ItemGroup").First();
        var originalElementCount = itemGroup.Elements().Count();
        var projectRef = doc.Descendants("ProjectReference").First();

        // Act
        _service.ConvertToPackageReference(projectRef, "MyProject", "1.2.3");

        // Assert
        var children = itemGroup.Elements().ToList();

        // Should maintain the same structure and element count
        itemGroup.Elements().Should().HaveCount(originalElementCount, "ItemGroup should have the same number of elements after conversion");

        // Check the converted element is in the correct position
        var convertedElement = children[1];
        convertedElement.Name.LocalName.Should().Be("PackageReference");
        convertedElement.Attribute("Include")?.Value.Should().Be("MyProject");
        convertedElement.Attribute("Version")?.Value.Should().Be("1.2.3");

        // Adjacent elements should remain unchanged
        children[0].Attribute("Include")?.Value.Should().Be("FluentAssertions");
        children[2].Attribute("Include")?.Value.Should().Be("xunit");
    }

    [Theory]
    [InlineData(@"..\MyProject\MyProject.csproj", @"C:\Projects\Solution\src\MyProject\MyProject.csproj")]
    [InlineData(@"..\..\Common\Shared.csproj", @"C:\Projects\Solution\Common\Shared.csproj")]
    [InlineData(@"..\Helpers\Helpers.csproj", @"C:\Projects\Solution\src\Helpers\Helpers.csproj")]
    public void GetRelativePath_ShouldCalculateCorrectRelativePath(string expectedPath, string absoluteProjectPath)
    {
        // Arrange
        var basePath = @"C:\Projects\Solution\src\Tests";

        // Act
        var result = _service.GetRelativePath(basePath, absoluteProjectPath);

        // Assert
        result.Should().Be(expectedPath);
    }

    [Fact]
    public void ConvertToPackageReference_WithFullPathResolvesCorrectly()
    {
        // Arrange
        var basePath = @"C:\Projects\Solution\src\Tests";
        var projectPath = @"C:\Projects\Solution\src\MyLib\MyLib.csproj";
        var expectedRelativePath = @"..\MyLib\MyLib.csproj";

        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <ProjectReference Include=""..\MyLib\MyLib.csproj"" />
  </ItemGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);
        var projectRef = doc.Descendants("ProjectReference").First();

        // Act - Calculate relative path to verify resolution
        var resolvedPath = _service.GetRelativePath(basePath, projectPath);
        _service.ConvertToPackageReference(projectRef, "MyLib", "1.0.0");

        // Assert
        resolvedPath.Should().Be(expectedRelativePath);
        projectRef.Name.LocalName.Should().Be("PackageReference");
        projectRef.Attribute("Include")?.Value.Should().Be("MyLib");
        projectRef.Attribute("Version")?.Value.Should().Be("1.0.0");
    }

    [Fact]
    public void GetBackedUpVersion_WithMultipleVersionBackups_ShouldReturnCorrectOne()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <VoloAbpVersion>8.2.0</VoloAbpVersion>
    <MicrosoftExtensionsVersion>8.0.0</MicrosoftExtensionsVersion>
    <SerilogVersion>3.1.1</SerilogVersion>
  </PropertyGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);

        // Act
        var voloAbpVersion = _service.GetBackedUpVersion(doc, "VoloAbp");
        var microsoftVersion = _service.GetBackedUpVersion(doc, "MicrosoftExtensions");
        var serilogVersion = _service.GetBackedUpVersion(doc, "Serilog");

        // Assert
        voloAbpVersion.Should().Be("8.2.0");
        microsoftVersion.Should().Be("8.0.0");
        serilogVersion.Should().Be("3.1.1");
    }

    [Fact]
    public void GetBackedUpUpVersion_NoBackupProperty_ShouldReturnNull()
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
        var version = _service.GetBackedUpVersion(doc, "MyLib");

        // Assert
        version.Should().BeNull();
    }

    [Fact]
    public void ConvertToPackageReference_MixedItemGroups_ShouldMaintainStructure()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""FluentAssertions"" Version=""8.8.0"" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include=""..\..\src\ProjectA\ProjectA.csproj"" />
    <ProjectReference Include=""..\..\src\ProjectB\ProjectB.csproj"" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include=""xunit"" Version=""2.6.11"" />
  </ItemGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);
        var projectRefs = doc.Descendants("ProjectReference").ToList();

        // Act
        _service.ConvertToPackageReference(projectRefs[0], "ProjectA", "1.0.0");
        _service.ConvertToPackageReference(projectRefs[1], "ProjectB", "2.0.0");

        // Assert
        var allItemGroups = doc.Descendants("ItemGroup").ToList();
        allItemGroups.Should().HaveCount(3);

        var middleItemGroup = allItemGroups[1];
        var convertedRefs = middleItemGroup.Elements("PackageReference").ToList();
        convertedRefs.Should().HaveCount(2);
        convertedRefs[0].Attribute("Include")?.Value.Should().Be("ProjectA");
        convertedRefs[1].Attribute("Include")?.Value.Should().Be("ProjectB");
    }
}
