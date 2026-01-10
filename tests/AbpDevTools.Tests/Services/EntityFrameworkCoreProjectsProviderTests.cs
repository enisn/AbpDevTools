using AbpDevTools.Configuration;
using AbpDevTools.Services;
using FluentAssertions;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AbpDevTools.Tests.Services;

/// <summary>
/// Unit tests for EntityFrameworkCoreProjectsProvider class.
/// Tests EF Core project detection and filtering logic.
/// </summary>
public class EntityFrameworkCoreProjectsProviderTests : IDisposable
{
    private readonly EntityFrameworkCoreProjectsProvider _provider;
    private readonly string _testDirectory;

    public EntityFrameworkCoreProjectsProviderTests()
    {
        var yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .Build();

        var yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .Build();

        var toolsConfiguration = new ToolsConfiguration(yamlDeserializer, yamlSerializer);
        var dependencyResolver = new DotnetDependencyResolver(toolsConfiguration);
        _provider = new EntityFrameworkCoreProjectsProvider(dependencyResolver);
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }

    [Fact]
    public async Task GetEfCoreProjectsAsync_WithEfCoreProject_ShouldReturnProject()
    {
        // Arrange
        var projectName = "MyProject.EntityFrameworkCore";
        var projectPath = CreateProjectFile(_testDirectory, projectName, includeEfCore: true);

        // Act
        var result = await _provider.GetEfCoreProjectsAsync(_testDirectory);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().ContainSingle(p => p.FullName == projectPath);
        result[0].Name.Should().Be($"{projectName}.csproj");
    }

    [Fact]
    public async Task GetEfCoreProjectsAsync_WithMultipleDbContextProjects_ShouldReturnAll()
    {
        // Arrange
        var project1Path = CreateProjectFile(_testDirectory, "Project1.EntityFrameworkCore", includeEfCore: true);
        var project2Path = CreateProjectFile(_testDirectory, "Project2.EntityFrameworkCore", includeEfCore: true);
        var project3Path = CreateProjectFile(_testDirectory, "Project3.EntityFrameworkCore", includeEfCore: true);

        // Act
        var result = await _provider.GetEfCoreProjectsAsync(_testDirectory);

        // Assert
        result.Should().HaveCount(3);
        result.Select(p => p.FullName).Should().Contain(new[] { project1Path, project2Path, project3Path });
    }

    [Fact]
    public async Task GetEfCoreProjectsAsync_WithNonEfCoreProject_ShouldSkipProject()
    {
        // Arrange
        var efProjectPath = CreateProjectFile(_testDirectory, "MyProject.EntityFrameworkCore", includeEfCore: true);
        var nonEfProjectPath = CreateProjectFile(_testDirectory, "MyProject.Domain", includeEfCore: false);

        // Act
        var result = await _provider.GetEfCoreProjectsAsync(_testDirectory);

        // Assert
        result.Should().ContainSingle();
        result[0].FullName.Should().Be(efProjectPath);
        result.Should().NotContain(p => p.FullName == nonEfProjectPath);
    }

    [Fact]
    public async Task GetEfCoreProjectsAsync_WithEmptySolution_ShouldReturnEmpty()
    {
        // Arrange - No projects created, empty directory

        // Act
        var result = await _provider.GetEfCoreProjectsAsync(_testDirectory);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEfCoreProjectsAsync_WithProjectFilters_ShouldFilterCorrectly()
    {
        // Arrange
        var efProjectPath1 = CreateProjectFile(_testDirectory, "MyProject.EntityFrameworkCore", includeEfCore: true);
        var efProjectPath2 = CreateProjectFile(_testDirectory, "MyProject.Tests", includeEfCore: true);
        var domainProjectPath = CreateProjectFile(_testDirectory, "MyProject.Domain", includeEfCore: false);

        // Act
        var result = await _provider.GetEfCoreProjectsAsync(_testDirectory, new[] { "EntityFrameworkCore" });

        // Assert
        result.Should().ContainSingle();
        result[0].FullName.Should().Be(efProjectPath1);
        result.Should().NotContain(p => p.FullName == efProjectPath2);
        result.Should().NotContain(p => p.FullName == domainProjectPath);
    }

    [Fact]
    public async Task GetEfCoreProjectsAsync_WithNestedProjects_ShouldFindAllProjects()
    {
        // Arrange
        var srcDir = Path.Combine(_testDirectory, "src");
        var testDir = Path.Combine(_testDirectory, "test");

        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(testDir);

        var project1Path = CreateProjectFile(srcDir, "MyProject.EntityFrameworkCore", includeEfCore: true);
        var project2Path = CreateProjectFile(testDir, "MyProject.Tests.EntityFrameworkCore", includeEfCore: true);

        // Act
        var result = await _provider.GetEfCoreProjectsAsync(_testDirectory);

        // Assert
        result.Should().HaveCount(2);
        result.Select(p => p.FullName).Should().Contain(new[] { project1Path, project2Path });
    }

    [Fact]
    public async Task GetEfCoreToolsProjectsAsync_WithToolsInstalled_ShouldReturnProject()
    {
        // Arrange
        var projectName = "MyProject.EntityFrameworkCore";
        var projectPath = CreateProjectFile(_testDirectory, projectName, includeEfCoreTools: true);

        // Act
        var result = await _provider.GetEfCoreToolsProjectsAsync(_testDirectory);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().ContainSingle(p => p.FullName == projectPath);
        result[0].Name.Should().Be($"{projectName}.csproj");
    }

    [Fact]
    public async Task GetEfCoreToolsProjectsAsync_WithoutToolsInstalled_ShouldReturnEmpty()
    {
        // Arrange
        var projectName = "MyProject.EntityFrameworkCore";
        CreateProjectFile(_testDirectory, projectName, includeEfCore: true, includeEfCoreTools: false);

        // Act
        var result = await _provider.GetEfCoreToolsProjectsAsync(_testDirectory);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEfCoreProjectsAsync_WithMultipleFilters_ShouldMatchAnyFilter()
    {
        // Arrange
        var project1Path = CreateProjectFile(_testDirectory, "MyProject.EntityFrameworkCore.Db1", includeEfCore: true);
        var project2Path = CreateProjectFile(_testDirectory, "MyProject.EntityFrameworkCore.Db2", includeEfCore: true);
        var project3Path = CreateProjectFile(_testDirectory, "MyProject.Domain", includeEfCore: true);

        // Act
        var result = await _provider.GetEfCoreProjectsAsync(_testDirectory, new[] { "Db1", "Db2" });

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(p => p.FullName == project1Path);
        result.Should().Contain(p => p.FullName == project2Path);
        result.Should().NotContain(p => p.FullName == project3Path);
    }

    [Fact]
    public async Task GetEfCoreProjectsAsync_DetectsEfCorePackage_ShouldReturnTrue()
    {
        // Arrange
        var projectName = "MyProject.Data";
        var projectPath = CreateProjectFile(_testDirectory, projectName, includeEfCore: true);

        // Act
        var result = await _provider.GetEfCoreProjectsAsync(_testDirectory);

        // Assert
        result.Should().ContainSingle();
        result[0].FullName.Should().Be(projectPath);
        File.ReadAllText(projectPath).Should().Contain("Microsoft.EntityFrameworkCore");
    }

    /// <summary>
    /// Creates a test project file with specified content.
    /// </summary>
    private string CreateProjectFile(string directory, string projectName, bool includeEfCore = false, bool includeEfCoreTools = false)
    {
        var projectDir = Path.Combine(directory, projectName);
        Directory.CreateDirectory(projectDir);

        var projectPath = Path.Combine(projectDir, $"{projectName}.csproj");
        var content = BuildProjectContent(projectName, includeEfCore, includeEfCoreTools);
        File.WriteAllText(projectPath, content);

        return projectPath;
    }

    /// <summary>
    /// Builds .csproj file content based on specified flags.
    /// </summary>
    private string BuildProjectContent(string projectName, bool includeEfCore, bool includeEfCoreTools)
    {
        var content = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
";

        if (includeEfCore)
        {
            content += @"  <ItemGroup>
    <PackageReference Include=""Microsoft.EntityFrameworkCore"" Version=""8.0.0"" />
  </ItemGroup>
";
        }

        if (includeEfCoreTools)
        {
            content += @"  <ItemGroup>
    <PackageReference Include=""Microsoft.EntityFrameworkCore.Tools"" Version=""8.0.0"" />
  </ItemGroup>
";
        }

        content += "</Project>";
        return content;
    }
}
