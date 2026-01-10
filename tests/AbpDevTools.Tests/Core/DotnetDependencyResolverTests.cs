using AbpDevTools.Configuration;
using AbpDevTools.Tests.Helpers;
using FluentAssertions;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AbpDevTools.Tests.Core;

/// <summary>
/// Unit tests for DotnetDependencyResolver class.
/// Tests NuGet package dependency resolution using dotnet CLI commands,
/// including package list parsing, project reference parsing, and direct
/// package reference detection.
/// </summary>
public class DotnetDependencyResolverTests : IDisposable
{
    private readonly string _testRootPath;
    private readonly ToolsConfiguration _toolsConfiguration;
    private readonly IDeserializer _yamlDeserializer;
    private readonly ISerializer _yamlSerializer;

    public DotnetDependencyResolverTests()
    {
        _testRootPath = Path.Combine(Path.GetTempPath(), $"DotnetDependencyResolverTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testRootPath);

        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .Build();

        _toolsConfiguration = new ToolsConfiguration(_yamlDeserializer, _yamlSerializer);
    }

    public void Dispose()
    {
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

    #region ParsePackageList Tests

    [Fact]
    public void ParsePackageList_ParsesDotnetListPackageOutput_Correctly()
    {
        // Arrange
        var jsonOutput = """
        {
          "projects": [
            {
              "path": "C:\\Projects\\Test\\Test.csproj",
              "frameworks": [
                {
                  "framework": "net8.0",
                  "topLevelPackages": [
                    {
                      "id": "Volo.Abp.Core",
                      "version": "8.2.0"
                    },
                    {
                      "id": "Microsoft.Extensions.Logging",
                      "version": "8.0.0"
                    },
                    {
                      "id": "Serilog.AspNetCore",
                      "version": "8.0.0"
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

        var resolver = CreateResolver();

        // Act
        var result = InvokeParsePackageList(resolver, jsonOutput);

        // Assert
        result.Should().HaveCount(3, "should parse 3 packages");
        result.Should().Contain("Volo.Abp.Core", "should contain ABP Core package");
        result.Should().Contain("Microsoft.Extensions.Logging", "should contain Microsoft.Extensions.Logging package");
        result.Should().Contain("Serilog.AspNetCore", "should contain Serilog.AspNetCore package");
    }

    [Fact]
    public void ParsePackageList_ReturnsPackageNames_AndVersions()
    {
        // Arrange
        var jsonOutput = """
        {
          "projects": [
            {
              "frameworks": [
                {
                  "framework": "net9.0",
                  "topLevelPackages": [
                    {
                      "id": "Newtonsoft.Json",
                      "version": "13.0.3"
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

        var resolver = CreateResolver();

        // Act
        var result = InvokeParsePackageList(resolver, jsonOutput);

        // Assert
        result.Should().ContainSingle("should have exactly one package");
        result.First().Should().Be("Newtonsoft.Json", "should return package name");
    }

    [Fact]
    public void ParsePackageList_HandlesTransitiveDependencies_FromMultipleFrameworks()
    {
        // Arrange
        var jsonOutput = """
        {
          "projects": [
            {
              "frameworks": [
                {
                  "framework": "net8.0",
                  "topLevelPackages": [
                    {
                      "id": "Volo.Abp.Core",
                      "version": "8.2.0"
                    }
                  ]
                },
                {
                  "framework": "net9.0",
                  "topLevelPackages": [
                    {
                      "id": "Volo.Abp.Core",
                      "version": "9.0.0"
                    },
                    {
                      "id": "Microsoft.Extensions.DependencyInjection",
                      "version": "9.0.0"
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

        var resolver = CreateResolver();

        // Act
        var result = InvokeParsePackageList(resolver, jsonOutput);

        // Assert
        // HashSet automatically deduplicates, so Volo.Abp.Core appearing in both frameworks counts once
        result.Should().HaveCount(2, "should parse unique packages from both frameworks");
        result.Should().Contain("Volo.Abp.Core");
        result.Should().Contain("Microsoft.Extensions.DependencyInjection");
    }

    [Fact]
    public void ParsePackageList_HandlesEmptyPackageList()
    {
        // Arrange
        var jsonOutput = """
        {
          "projects": [
            {
              "frameworks": [
                {
                  "framework": "net8.0",
                  "topLevelPackages": []
                }
              ]
            }
          ]
        }
        """;

        var resolver = CreateResolver();

        // Act
        var result = InvokeParsePackageList(resolver, jsonOutput);

        // Assert
        result.Should().BeEmpty("should return empty set for no packages");
    }

    #endregion

    #region ParseProjectReferences Tests

    [Fact]
    public void ParseProjectReferences_ParsesDotnetListReferenceOutput_Correctly()
    {
        // Arrange
        var output = """
        C:\Projects\Test\Domain\Domain.csproj
        C:\Projects\Test\Application\Application.csproj
        C:\Projects\Test\Shared\Shared.csproj
        """;

        var resolver = CreateResolver();

        // Act
        var result = InvokeParseProjectReferences(resolver, output);

        // Assert
        result.Should().HaveCount(3, "should parse 3 project references");
        result.Should().Contain("Domain", "should extract Domain project name");
        result.Should().Contain("Application", "should extract Application project name");
        result.Should().Contain("Shared", "should extract Shared project name");
    }

    [Fact]
    public void ParseProjectReferences_HandlesVariousOutputFormats()
    {
        // Arrange
        var outputs = new[]
        {
            "C:\\Projects\\Test\\Domain\\Domain.csproj",
            """
            C:\Projects\Test\Domain\Domain.csproj
            C:\Projects\Test\Application\Application.csproj
            """,
            "/mnt/c/Projects/Test/Domain/Domain.csproj"
        };

        var resolver = CreateResolver();

        foreach (var output in outputs)
        {
            // Act
            var result = InvokeParseProjectReferences(resolver, output);

            // Assert
            result.Should().Contain("Domain", $"should parse Domain from format");
        }
    }

    [Fact]
    public void ParseProjectReferences_HandlesEmptyOutput()
    {
        // Arrange
        var output = "";

        var resolver = CreateResolver();

        // Act
        var result = InvokeParseProjectReferences(resolver, output);

        // Assert
        result.Should().BeEmpty("should return empty set for no references");
    }

    [Fact]
    public void ParseProjectReferences_HandlesMixedPathFormats()
    {
        // Arrange
        var output = """
        C:\Projects\Test\Domain\Domain.csproj
        /mnt/c/Projects/Test/Application/Application.csproj
        ..\Shared\Shared.csproj
        """;

        var resolver = CreateResolver();

        // Act
        var result = InvokeParseProjectReferences(resolver, output);

        // Assert
        result.Should().HaveCountGreaterThanOrEqualTo(2, "should parse multiple path formats");
    }

    #endregion

    #region HasDirectPackageReferenceAsync Tests

    [Fact]
    public async Task HasDirectPackageReference_ReturnsTrue_ForDirectReferences()
    {
        // Arrange
        var csprojContent = """
        <Project Sdk="Microsoft.NET.Sdk">
          <ItemGroup>
            <PackageReference Include="Volo.Abp.Core" Version="8.2.0" />
            <PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
          </ItemGroup>
        </Project>
        """;

        var projectPath = CreateTestCsprojFile("TestProject.csproj", csprojContent);
        var resolver = CreateResolver();

        // Act
        var result = await resolver.HasDirectPackageReferenceAsync(projectPath, "Volo.Abp.Core", CancellationToken.None);

        // Assert
        result.Should().BeTrue("should find direct package reference");
    }

    [Fact]
    public async Task HasDirectPackageReference_ReturnsFalse_ForTransitiveReferences()
    {
        // Arrange
        var csprojContent = """
        <Project Sdk="Microsoft.NET.Sdk">
          <ItemGroup>
            <PackageReference Include="Volo.Abp.Core" Version="8.2.0" />
          </ItemGroup>
        </Project>
        """;

        var projectPath = CreateTestCsprojFile("TestProject.csproj", csprojContent);
        var resolver = CreateResolver();

        // Act
        // Microsoft.Extensions.Logging is a transitive dependency of Volo.Abp.Core, not a direct reference
        var result = await resolver.HasDirectPackageReferenceAsync(projectPath, "Microsoft.Extensions.Logging", CancellationToken.None);

        // Assert
        result.Should().BeFalse("should not find transitive package reference");
    }

    [Fact]
    public async Task HasDirectPackageReference_ReturnsFalse_ForMissingPackage()
    {
        // Arrange
        var csprojContent = """
        <Project Sdk="Microsoft.NET.Sdk">
          <ItemGroup>
            <PackageReference Include="Volo.Abp.Core" Version="8.2.0" />
          </ItemGroup>
        </Project>
        """;

        var projectPath = CreateTestCsprojFile("TestProject.csproj", csprojContent);
        var resolver = CreateResolver();

        // Act
        var result = await resolver.HasDirectPackageReferenceAsync(projectPath, "NonExistent.Package", CancellationToken.None);

        // Assert
        result.Should().BeFalse("should not find missing package");
    }

    [Fact]
    public async Task HasDirectPackageReference_HandlesMissingProjectFile()
    {
        // Arrange
        var missingProjectPath = Path.Combine(_testRootPath, "NonExistent.csproj");
        var resolver = CreateResolver();

        // Act
        var result = await resolver.HasDirectPackageReferenceAsync(missingProjectPath, "Any.Package", CancellationToken.None);

        // Assert
        result.Should().BeFalse("should return false for missing project file");
    }

    #endregion

    #region Helper Methods

    private DotnetDependencyResolver CreateResolver()
    {
        return new DotnetDependencyResolver(_toolsConfiguration);
    }

    private string CreateTestCsprojFile(string fileName, string content)
    {
        var filePath = Path.Combine(_testRootPath, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    private HashSet<string> InvokeParsePackageList(DotnetDependencyResolver resolver, string jsonOutput)
    {
        // Use reflection to call the private method
        var method = typeof(DotnetDependencyResolver).GetMethod("ParsePackageList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method == null)
        {
            throw new InvalidOperationException("ParsePackageList method not found");
        }
        return (HashSet<string>)method.Invoke(resolver, new object[] { jsonOutput })!;
    }

    private HashSet<string> InvokeParseProjectReferences(DotnetDependencyResolver resolver, string output)
    {
        // Use reflection to call the private method
        var method = typeof(DotnetDependencyResolver).GetMethod("ParseProjectReferences", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method == null)
        {
            throw new InvalidOperationException("ParseProjectReferences method not found");
        }
        return (HashSet<string>)method.Invoke(resolver, new object[] { output })!;
    }

    #endregion
}
