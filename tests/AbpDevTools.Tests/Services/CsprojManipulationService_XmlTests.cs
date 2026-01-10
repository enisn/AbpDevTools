using System.Xml.Linq;
using AbpDevTools.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AbpDevTools.Tests.Services;

/// <summary>
/// Unit tests for CsprojManipulationService XML parsing functionality.
/// Tests .csproj file XML manipulation including PackageReference and ProjectReference handling.
/// </summary>
public class CsprojManipulationService_XmlTests
{
    private readonly CsprojManipulationService _service;
    private readonly FileExplorer _mockFileExplorer;

    public CsprojManipulationService_XmlTests()
    {
        _mockFileExplorer = Substitute.For<FileExplorer>();
        _service = new CsprojManipulationService(_mockFileExplorer);
    }

    [Fact]
    public void LoadAndParse_ValidCsprojXml_ShouldSucceed()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>";

        // Act
        var doc = XDocument.Parse(csprojXml);

        // Assert
        doc.Should().NotBeNull();
        doc.Root.Should().NotBeNull();
        doc.Root!.Name.LocalName.Should().Be("Project");
        doc.Root.Attribute("Sdk")?.Value.Should().Be("Microsoft.NET.Sdk");
    }

    [Fact]
    public void ExtractPackageReferences_SinglePackage_ShouldReturnNameAndVersion()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""FluentAssertions"" Version=""8.8.0"" />
  </ItemGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);

        // Act
        var packageRefs = doc.Descendants("PackageReference").ToList();

        // Assert
        packageRefs.Should().HaveCount(1);
        packageRefs[0].Attribute("Include")?.Value.Should().Be("FluentAssertions");
        packageRefs[0].Attribute("Version")?.Value.Should().Be("8.8.0");
    }

    [Fact]
    public void ExtractPackageReferences_MultiplePackages_ShouldReturnAll()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""FluentAssertions"" Version=""8.8.0"" />
    <PackageReference Include=""xunit"" Version=""2.6.11"" />
    <PackageReference Include=""NSubstitute"" Version=""5.1.0"" />
  </ItemGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);

        // Act
        var packageRefs = doc.Descendants("PackageReference").ToList();

        // Assert
        packageRefs.Should().HaveCount(3);
        packageRefs[0].Attribute("Include")?.Value.Should().Be("FluentAssertions");
        packageRefs[1].Attribute("Include")?.Value.Should().Be("xunit");
        packageRefs[2].Attribute("Include")?.Value.Should().Be("NSubstitute");
    }

    [Fact]
    public void ExtractProjectReferences_SingleProject_ShouldReturnPath()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <ProjectReference Include=""..\..\src\MyProject\MyProject.csproj"" />
  </ItemGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);

        // Act
        var projectRefs = _service.GetAllProjectReferences(doc);

        // Assert
        projectRefs.Should().HaveCount(1);
        projectRefs[0].Attribute("Include")?.Value.Should().Be(@"..\..\src\MyProject\MyProject.csproj");
    }

    [Fact]
    public void HandleEmptyProjectFile_ShouldReturnDocumentWithNoReferences()
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
        var packageRefs = doc.Descendants("PackageReference").ToList();
        var projectRefs = _service.GetAllProjectReferences(doc);

        // Assert
        packageRefs.Should().BeEmpty();
        projectRefs.Should().BeEmpty();
    }

    [Fact]
    public void HandleProjectWithNoReferences_ShouldReturnEmptyLists()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);

        // Act
        var packageRefs = _service.GetMatchingPackageReferences(doc, "*");
        var projectRefs = _service.GetAllProjectReferences(doc);

        // Assert
        packageRefs.Should().BeEmpty("Should have no PackageReference elements");
        projectRefs.Should().BeEmpty("Should have no ProjectReference elements");
    }

    [Fact]
    public void HandleMixedReferences_PackageAndProject_ShouldExtractBoth()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""FluentAssertions"" Version=""8.8.0"" />
    <PackageReference Include=""xunit"" Version=""2.6.11"" />
    <ProjectReference Include=""..\..\src\AbpDevTools\AbpDevTools.csproj"" />
    <PackageReference Include=""NSubstitute"" Version=""5.1.0"" />
    <ProjectReference Include=""..\Common\Shared.csproj"" />
  </ItemGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);

        // Act
        var packageRefs = doc.Descendants("PackageReference").ToList();
        var projectRefs = _service.GetAllProjectReferences(doc);

        // Assert
        packageRefs.Should().HaveCount(3, "Should have 3 PackageReference elements");
        packageRefs[0].Attribute("Include")?.Value.Should().Be("FluentAssertions");
        packageRefs[1].Attribute("Include")?.Value.Should().Be("xunit");
        packageRefs[2].Attribute("Include")?.Value.Should().Be("NSubstitute");

        projectRefs.Should().HaveCount(2, "Should have 2 ProjectReference elements");
        projectRefs[0].Attribute("Include")?.Value.Should().Be(@"..\..\src\AbpDevTools\AbpDevTools.csproj");
        projectRefs[1].Attribute("Include")?.Value.Should().Be(@"..\Common\Shared.csproj");
    }

    [Fact]
    public void HandleXmlNamespaces_WithDefaultNamespace_ShouldParseCorrectly()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <ItemGroup>
    <PackageReference Include=""FluentAssertions"" Version=""8.8.0"" />
  </ItemGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);

        // Act - Using Descendants with namespace
        var ns = doc.Root?.GetDefaultNamespace();
        var packageRefs = ns != null
            ? doc.Descendants(ns + "PackageReference").ToList()
            : doc.Descendants("PackageReference").ToList();

        // Assert
        packageRefs.Should().HaveCount(1);
        packageRefs[0].Attribute("Include")?.Value.Should().Be("FluentAssertions");
        packageRefs[0].Attribute("Version")?.Value.Should().Be("8.8.0");
    }

    [Fact]
    public void HandleInvalidXml_ShouldThrowXmlException()
    {
        // Arrange
        var invalidXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup
  <!-- Missing closing tag for Project -->
";

        // Act & Assert
        Assert.Throws<System.Xml.XmlException>(() => XDocument.Parse(invalidXml));
    }

    [Fact]
    public void GetMatchingPackageReferences_WithWildcardPattern_ShouldMatchPrefix()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Microsoft.Extensions.Logging"" Version=""8.0.0"" />
    <PackageReference Include=""Microsoft.Extensions.DependencyInjection"" Version=""8.0.0"" />
    <PackageReference Include=""FluentAssertions"" Version=""8.8.0"" />
  </ItemGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);

        // Act
        var matchingRefs = _service.GetMatchingPackageReferences(doc, "Microsoft.Extensions.*");

        // Assert
        matchingRefs.Should().HaveCount(2);
        matchingRefs[0].Attribute("Include")?.Value.Should().StartWith("Microsoft.Extensions.");
        matchingRefs[1].Attribute("Include")?.Value.Should().StartWith("Microsoft.Extensions.");
    }

    [Fact]
    public void GetMatchingPackageReferences_WithExactPattern_ShouldMatchExactName()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""FluentAssertions"" Version=""8.8.0"" />
    <PackageReference Include=""xunit"" Version=""2.6.11"" />
    <PackageReference Include=""NSubstitute"" Version=""5.1.0"" />
  </ItemGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);

        // Act
        var matchingRefs = _service.GetMatchingPackageReferences(doc, "FluentAssertions");

        // Assert
        matchingRefs.Should().HaveCount(1);
        matchingRefs[0].Attribute("Include")?.Value.Should().Be("FluentAssertions");
    }

    [Fact]
    public void GetMatchingPackageReferences_CaseInsensitive_ShouldMatchRegardlessOfCase()
    {
        // Arrange
        var csprojXml = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""FluentAssertions"" Version=""8.8.0"" />
    <PackageReference Include=""fluentassertions.core"" Version=""8.8.0"" />
  </ItemGroup>
</Project>";
        var doc = XDocument.Parse(csprojXml);

        // Act
        var matchingRefs = _service.GetMatchingPackageReferences(doc, "FLUENTASSERTIONS");

        // Assert
        matchingRefs.Should().HaveCount(1);
        matchingRefs[0].Attribute("Include")?.Value.Should().Be("FluentAssertions");
    }
}
