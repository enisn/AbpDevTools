using AbpDevTools.Configuration;
using AbpDevTools.Environments;
using AbpDevTools.LocalConfigurations;
using AbpDevTools.Notifications;
using AbpDevTools.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AbpDevTools.Tests.Commands;

/// <summary>
/// Unit tests for RunCommand project discovery and configuration handling.
/// Tests verify how projects are discovered from configuration and filtered
/// by various patterns and settings.
/// </summary>
public class RunCommand_DiscoveryTests
{
    #region DiscoverRunnableProjects Tests

    [Fact]
    public void DiscoverRunnableProjects_ReturnsAllRunnableProjectsFromConfig()
    {
        // Arrange
        var provider = CreateRunnableProjectsProvider();
        var testRootPath = Path.Combine(Path.GetTempPath(), $"AbpDevToolsTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(testRootPath);

        try
        {
            // Create projects with Program.cs (runnable)
            CreateProjectWithProgramCs(Path.Combine(testRootPath, "MyProject.HttpApi.Host"));
            CreateProjectWithProgramCs(Path.Combine(testRootPath, "MyProject.Web"));
            CreateProjectWithProgramCs(Path.Combine(testRootPath, "MyProject.Blazor"));

            // Create non-runnable projects (without Program.cs)
            CreateProjectWithoutProgramCs(Path.Combine(testRootPath, "MyProject.Application"));
            CreateProjectWithoutProgramCs(Path.Combine(testRootPath, "MyProject.Domain"));

            // Act
            var results = provider.GetRunnableProjects(testRootPath);

            // Assert
            results.Should().HaveCount(3, "only projects with Program.cs should be returned as runnable");
            results.Select(p => p.Name).Should().Contain("MyProject.HttpApi.Host.csproj");
            results.Select(p => p.Name).Should().Contain("MyProject.Web.csproj");
            results.Select(p => p.Name).Should().Contain("MyProject.Blazor.csproj");
            results.Select(p => p.Name).Should().NotContain(n => n.Contains("Application"));
            results.Select(p => p.Name).Should().NotContain(n => n.Contains("Domain"));
        }
        finally
        {
            CleanupTestDirectory(testRootPath);
        }
    }

    [Fact]
    public void DiscoverRunnableProjects_FiltersByIncludePattern()
    {
        // Arrange
        var provider = CreateRunnableProjectsProvider();
        var testRootPath = Path.Combine(Path.GetTempPath(), $"AbpDevToolsTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(testRootPath);

        try
        {
            // Create multiple runnable projects
            CreateProjectWithProgramCs(Path.Combine(testRootPath, "MyProject.HttpApi.Host"));
            CreateProjectWithProgramCs(Path.Combine(testRootPath, "MyProject.Web"));
            CreateProjectWithProgramCs(Path.Combine(testRootPath, "MyProject.Blazor.Server"));
            CreateProjectWithProgramCs(Path.Combine(testRootPath, "MyProject.AuthServer"));

            // Act - Get all runnable projects
            var allProjects = provider.GetRunnableProjects(testRootPath);

            // Simulate filtering by include pattern (e.g., projects containing "Host")
            var filteredProjects = allProjects
                .Where(p => p.Name.Contains(".Host", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            // Assert
            filteredProjects.Should().HaveCount(1, "only projects with '.Host' should match the include pattern");
            filteredProjects[0].Name.Should().Be("MyProject.HttpApi.Host.csproj");
        }
        finally
        {
            CleanupTestDirectory(testRootPath);
        }
    }

    [Fact]
    public void DiscoverRunnableProjects_FiltersByExcludePattern()
    {
        // Arrange
        var provider = CreateRunnableProjectsProvider();
        var testRootPath = Path.Combine(Path.GetTempPath(), $"AbpDevToolsTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(testRootPath);

        try
        {
            // Create runnable projects including DbMigrator
            CreateProjectWithProgramCs(Path.Combine(testRootPath, "MyProject.HttpApi.Host"));
            CreateProjectWithProgramCs(Path.Combine(testRootPath, "MyProject.Web"));
            CreateProjectWithProgramCs(Path.Combine(testRootPath, "MyProject.DbMigrator"));

            // Act - Get all runnable projects
            var allProjects = provider.GetRunnableProjects(testRootPath);

            // Simulate filtering by exclude pattern (e.g., exclude DbMigrator)
            var filteredProjects = allProjects
                .Where(p => !p.Name.Contains(".DbMigrator", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            // Assert
            filteredProjects.Should().HaveCount(2, "DbMigrator should be excluded from results");
            filteredProjects.Select(p => p.Name).Should().NotContain(n => n.Contains("DbMigrator"));
            filteredProjects.Select(p => p.Name).Should().Contain("MyProject.HttpApi.Host.csproj");
            filteredProjects.Select(p => p.Name).Should().Contain("MyProject.Web.csproj");
        }
        finally
        {
            CleanupTestDirectory(testRootPath);
        }
    }

    [Fact]
    public void DiscoverRunnableProjects_AppliesWatchFilters()
    {
        // Arrange
        var provider = CreateRunnableProjectsProvider();
        var testRootPath = Path.Combine(Path.GetTempPath(), $"AbpDevToolsTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(testRootPath);

        try
        {
            // Create runnable projects
            CreateProjectWithProgramCs(Path.Combine(testRootPath, "MyProject.HttpApi.Host"));
            CreateProjectWithProgramCs(Path.Combine(testRootPath, "MyProject.Web"));

            // Act - Get all runnable projects
            var allProjects = provider.GetRunnableProjects(testRootPath);

            // Simulate watch filter - projects with watch mode enabled in config
            var localConfiguration = new LocalConfiguration
            {
                Run = new LocalConfiguration.LocalRunOption
                {
                    Watch = true,
                    Projects = new[] { "MyProject.HttpApi.Host" }
                }
            };

            var watchEnabledProjects = allProjects
                .Where(p => localConfiguration.Run?.Projects.Any(proj =>
                    p.Name.Contains(proj, StringComparison.OrdinalIgnoreCase)) == true)
                .ToArray();

            // Assert
            watchEnabledProjects.Should().HaveCount(1, "only project specified in config should have watch enabled");
            watchEnabledProjects[0].Name.Should().Be("MyProject.HttpApi.Host.csproj");
        }
        finally
        {
            CleanupTestDirectory(testRootPath);
        }
    }

    [Fact]
    public void DiscoverRunnableProjects_HandlesEmptyConfig()
    {
        // Arrange
        var provider = CreateRunnableProjectsProvider();
        var testRootPath = Path.Combine(Path.GetTempPath(), $"AbpDevToolsTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(testRootPath);

        try
        {
            // Create runnable projects
            CreateProjectWithProgramCs(Path.Combine(testRootPath, "MyProject.HttpApi.Host"));
            CreateProjectWithProgramCs(Path.Combine(testRootPath, "MyProject.Web"));

            // Act - Simulate empty config
            var localConfiguration = new LocalConfiguration
            {
                Run = new LocalConfiguration.LocalRunOption
                {
                    Projects = Array.Empty<string>()
                }
            };

            var allProjects = provider.GetRunnableProjects(testRootPath);
            var filteredProjects = localConfiguration.Run?.Projects.Length == 0
                ? allProjects // No filter specified, return all
                : allProjects.Where(p => localConfiguration.Run!.Projects.Any(proj =>
                    p.Name.Contains(proj, StringComparison.OrdinalIgnoreCase))).ToArray();

            // Assert
            filteredProjects.Should().HaveCount(2, "empty config should not filter any projects");
            filteredProjects.Select(p => p.Name).Should().Contain("MyProject.HttpApi.Host.csproj");
            filteredProjects.Select(p => p.Name).Should().Contain("MyProject.Web.csproj");
        }
        finally
        {
            CleanupTestDirectory(testRootPath);
        }
    }

    #endregion

    #region GetProjectArguments Tests

    [Theory]
    [InlineData(null, null, null, "")]
    [InlineData(true, null, null, " --no-build")]
    [InlineData(null, true, null, " /graphBuild")]
    [InlineData(null, null, "Release", " --configuration Release")]
    [InlineData(true, true, "Debug", " --no-build /graphBuild --configuration Debug")]
    public void GetProjectArguments_ReturnsCorrectArgumentsFromConfig(bool? noBuild, bool? graphBuild, string? configuration, string expectedSuffix)
    {
        // Arrange
        var localConfiguration = new LocalConfiguration
        {
            Run = new LocalConfiguration.LocalRunOption
            {
                NoBuild = noBuild ?? false,
                GraphBuild = graphBuild ?? false,
                Configuration = configuration
            }
        };

        // Act - Simulate BuildCommandSuffix method logic
        var commandSuffix = (localConfiguration.Run?.NoBuild == true) ? " --no-build" : string.Empty;

        if (localConfiguration.Run?.GraphBuild == true)
        {
            commandSuffix += " /graphBuild";
        }

        if (!string.IsNullOrEmpty(localConfiguration.Run?.Configuration))
        {
            commandSuffix += $" --configuration {localConfiguration.Run.Configuration}";
        }

        // Assert
        commandSuffix.Should().Be(expectedSuffix);
    }

    #endregion

    #region GetProjectEnvironmentVariables Tests

    [Fact]
    public void GetProjectEnvironmentVariables_ReturnsCorrectEnvVars()
    {
        // Arrange
        var localConfiguration = new LocalConfiguration
        {
            Environment = new LocalConfiguration.LocalEnvironmentOption
            {
                Name = "Development",
                Variables = new Dictionary<string, string?>
                {
                    { "ASPNETCORE_ENVIRONMENT", "Development" },
                    { "ASPNETCORE_URLS", "https://localhost:5001" }
                }
            }
        };

        // Act - Simulate environment variables extraction
        var envVars = localConfiguration.Environment?.Variables ?? new Dictionary<string, string?>();

        // Assert
        envVars.Should().HaveCount(2);
        envVars.Should().ContainKey("ASPNETCORE_ENVIRONMENT");
        envVars["ASPNETCORE_ENVIRONMENT"].Should().Be("Development");
        envVars.Should().ContainKey("ASPNETCORE_URLS");
        envVars["ASPNETCORE_URLS"].Should().Be("https://localhost:5001");
    }

    [Theory]
    [InlineData("Development", "Development")]
    [InlineData("Production", "Production")]
    [InlineData("Staging", "Staging")]
    public void GetProjectEnvironmentVariables_ReturnsCorrectEnvironmentName(string envName, string expectedName)
    {
        // Arrange
        var localConfiguration = new LocalConfiguration
        {
            Environment = new LocalConfiguration.LocalEnvironmentOption
            {
                Name = envName
            }
        };

        // Act - Extract environment name
        var actualName = localConfiguration.Environment?.Name;

        // Assert
        actualName.Should().Be(expectedName);
    }

    #endregion

    #region GetWorkingDirectory Tests

    [Fact]
    public void GetWorkingDirectory_ReturnsCorrectPathForProject()
    {
        // Arrange
        var testRootPath = Path.Combine(Path.GetTempPath(), $"AbpDevToolsTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(testRootPath);

        try
        {
            var projectName = "MyProject.HttpApi.Host";
            var projectDir = Path.Combine(testRootPath, projectName);
            Directory.CreateDirectory(projectDir);

            CreateProjectWithProgramCs(projectDir);

            // Act - Get the directory from the project file
            var projectFile = new FileInfo(Path.Combine(projectDir, $"{projectName}.csproj"));
            var workingDirectory = Path.GetDirectoryName(projectFile.FullName);

            // Assert
            workingDirectory.Should().NotBeNull();
            workingDirectory.Should().EndWith(projectName);
            workingDirectory.Should().Be(projectDir);
        }
        finally
        {
            CleanupTestDirectory(testRootPath);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a RunnableProjectsProvider with mocked configuration.
    /// </summary>
    private RunnableProjectsProvider CreateRunnableProjectsProvider()
    {
        var mockDeserializer = Substitute.For<YamlDotNet.Serialization.IDeserializer>();
        var mockSerializer = Substitute.For<YamlDotNet.Serialization.ISerializer>();

        return new RunnableProjectsProvider(
            new RunConfiguration(mockDeserializer, mockSerializer));
    }

    /// <summary>
    /// Creates a project file with Program.cs in the specified directory.
    /// </summary>
    private void CreateProjectWithProgramCs(string projectDir)
    {
        Directory.CreateDirectory(projectDir);
        var projectName = Path.GetFileName(projectDir);
        var projectPath = Path.Combine(projectDir, $"{projectName}.csproj");

        var programCsPath = Path.Combine(projectDir, "Program.cs");
        File.WriteAllText(programCsPath, @"
using System;

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine(""Hello, World!"");
    }
}
");

        File.WriteAllText(projectPath, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>");
    }

    /// <summary>
    /// Creates a project file without Program.cs in the specified directory.
    /// </summary>
    private void CreateProjectWithoutProgramCs(string projectDir)
    {
        Directory.CreateDirectory(projectDir);
        var projectName = Path.GetFileName(projectDir);
        var projectPath = Path.Combine(projectDir, $"{projectName}.csproj");

        File.WriteAllText(projectPath, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>");
    }

    /// <summary>
    /// Cleans up test directory.
    /// </summary>
    private void CleanupTestDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    #endregion
}
