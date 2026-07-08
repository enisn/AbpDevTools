using AbpDevTools.Configuration;
using AbpDevTools.Services;
using AbpDevTools.Tests.Helpers;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AbpDevTools.Tests.Services;

/// <summary>
/// Unit tests for RunnableProjectsProvider class.
/// Tests project discovery logic including runnable project detection,
/// DbMigrator identification, and file system operations.
/// </summary>
public class RunnableProjectsProviderTests : CommandTestBase
{
    private readonly string _testRootPath;

    public RunnableProjectsProviderTests()
    {
        // Create a temporary directory for test files
        _testRootPath = Path.Combine(Path.GetTempPath(), $"AbpDevToolsTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testRootPath);
    }

    public override void Dispose()
    {
        base.Dispose();

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

    #region GetRunnableProjects Tests

    [Fact]
    public void GetRunnableProjects_WithMultipleProjects_ReturnsOnlyProjectsWithProgramCs()
    {
        // Arrange
        var provider = CreateProvider();

        // Create multiple projects, some with Program.cs, some without
        CreateProjectWithProgramCs("MyProject.HttpApi.Host");
        CreateProjectWithProgramCs("MyProject.Web");
        CreateProjectWithoutProgramCs("MyProject.Application");
        CreateProjectWithoutProgramCs("MyProject.Domain");

        // Act
        var results = provider.GetRunnableProjects(_testRootPath);

        // Assert
        results.Should().HaveCount(2, "only projects with Program.cs should be returned");
        results.Should().ContainSingle(p => p.FullName.Contains("MyProject.HttpApi.Host"));
        results.Should().ContainSingle(p => p.FullName.Contains("MyProject.Web"));
    }

    [Fact]
    public void GetRunnableProjects_WithEmptySolution_ReturnsEmptyArray()
    {
        // Arrange
        var provider = CreateProvider();
        // Don't create any projects - empty directory

        // Act
        var results = provider.GetRunnableProjects(_testRootPath);

        // Assert
        results.Should().BeEmpty("solution contains no projects");
    }

    [Fact]
    public void GetRunnableProjects_ReturnsProjectsWithCorrectPathsAndNames()
    {
        // Arrange
        var provider = CreateProvider();
        var projectName = "MyProject.HttpApi.Host";
        var projectDir = CreateProjectWithProgramCs(projectName);

        // Act
        var results = provider.GetRunnableProjects(_testRootPath);

        // Assert
        results.Should().HaveCount(1);
        var project = results[0];
        project.FullName.Should().EndWith($"{projectName}\\{projectName}.csproj");
        project.Name.Should().Be($"{projectName}.csproj");
        project.DirectoryName.Should().EndWith(projectName);
    }

    [Fact]
    public void GetRunnableProjects_WithNestedProjects_FindsAllRunnableProjects()
    {
        // Arrange
        var provider = CreateProvider();

        // Create projects in different nested directories
        Directory.CreateDirectory(Path.Combine(_testRootPath, "apps", "App1"));
        CreateProjectFileWithProgramCs(Path.Combine(_testRootPath, "apps", "App1", "App1.csproj"));

        Directory.CreateDirectory(Path.Combine(_testRootPath, "apps", "App2"));
        CreateProjectFileWithProgramCs(Path.Combine(_testRootPath, "apps", "App2", "App2.csproj"));

        Directory.CreateDirectory(Path.Combine(_testRootPath, "services", "Service1"));
        CreateProjectFileWithProgramCs(Path.Combine(_testRootPath, "services", "Service1", "Service1.csproj"));

        Directory.CreateDirectory(Path.Combine(_testRootPath, "src", "Library"));
        CreateProjectFileWithoutProgramCs(Path.Combine(_testRootPath, "src", "Library", "Library.csproj"));

        // Act
        var results = provider.GetRunnableProjects(_testRootPath);

        // Assert
        results.Should().HaveCount(3, "should find all runnable projects in nested directories");
        results.Should().OnlyContain(p => p.Directory != null);
    }

    #endregion

    #region GetRunnableNpmProjects Tests

    [Fact]
    public void GetRunnableNpmProjects_WithDevScript_ReturnsNpmApp()
    {
        // Arrange
        var provider = CreateProvider();
        var appDir = CreatePackageJson("apps/react", """
        {
          "name": "react-client",
          "scripts": {
            "dev": "vite --host 0.0.0.0"
          }
        }
        """);

        // Act
        var results = provider.GetRunnableNpmProjects(_testRootPath);

        // Assert
        results.Should().HaveCount(1);
        results[0].Type.Should().Be(RunnableAppType.Npm);
        results[0].Name.Should().Be("react-client:dev");
        results[0].FullName.Should().Be(Path.Combine(appDir, "package.json"));
        results[0].WorkingDirectory.Should().Be(appDir);
        results[0].Script.Should().Be("dev");
        results[0].PackageManager.Should().Be("npm");
        results[0].IsRunByDefault.Should().BeTrue();
    }

    [Fact]
    public void GetRunnableNpmProjects_WithAngularStartScript_ReturnsDefaultRunnableApp()
    {
        // Arrange
        var provider = CreateProvider();
        CreatePackageJson("angular", """
        {
          "scripts": {
            "start": "ng serve --host 0.0.0.0"
          }
        }
        """);

        // Act
        var results = provider.GetRunnableNpmProjects(_testRootPath);

        // Assert
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("angular:start");
        results[0].Script.Should().Be("start");
        results[0].IsRunByDefault.Should().BeTrue("ng serve is a known web server script");
    }

    [Fact]
    public void GetRunnableNpmProjects_WithUnknownStartScript_ReturnsAmbiguousApp()
    {
        // Arrange
        var provider = CreateProvider();
        CreatePackageJson("tools/worker", """
        {
          "scripts": {
            "start": "node worker.js"
          }
        }
        """);

        // Act
        var results = provider.GetRunnableNpmProjects(_testRootPath);

        // Assert
        results.Should().HaveCount(1);
        results[0].Script.Should().Be("start");
        results[0].IsRunByDefault.Should().BeFalse("unknown start scripts should require explicit selection");
    }

    [Fact]
    public void GetRunnableNpmProjects_WithConfiguredScript_ReturnsCustomScript()
    {
        // Arrange
        var provider = CreateProvider();
        CreatePackageJson("custom-client", """
        {
          "scripts": {
            "web": "vite --host 0.0.0.0"
          }
        }
        """);

        // Act
        var results = provider.GetRunnableNpmProjects(_testRootPath, new[] { "web" });

        // Assert
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("custom-client:web");
        results[0].Script.Should().Be("web");
        results[0].IsRunByDefault.Should().BeTrue("configured scripts are explicit opt-ins");
    }

    [Fact]
    public void GetRunnableNpmProjects_UsesPackageManagerField()
    {
        // Arrange
        var provider = CreateProvider();
        CreatePackageJson("vue", """
        {
          "packageManager": "pnpm@9.0.0",
          "scripts": {
            "serve": "vue-cli-service serve"
          }
        }
        """);

        // Act
        var results = provider.GetRunnableNpmProjects(_testRootPath);

        // Assert
        results.Should().HaveCount(1);
        results[0].PackageManager.Should().Be("pnpm");
    }

    [Fact]
    public void GetRunnableNpmProjects_DefaultsToNpmWhenOnlyLockFileExists()
    {
        // Arrange
        var provider = CreateProvider();
        var appDir = CreatePackageJson("yarn-client", """
        {
          "scripts": {
            "dev": "vite"
          }
        }
        """);
        File.WriteAllText(Path.Combine(appDir, "yarn.lock"), string.Empty);

        // Act
        var results = provider.GetRunnableNpmProjects(_testRootPath);

        // Assert
        results.Should().HaveCount(1);
        results[0].PackageManager.Should().Be("npm", "lockfiles can be stale and should not override the default script runner");
    }

    [Fact]
    public void GetRunnableNpmProjects_IgnoresGeneratedPackageJsonFiles()
    {
        // Arrange
        var provider = CreateProvider();
        CreatePackageJson(Path.Combine("node_modules", "vite"), """
        {
          "scripts": {
            "dev": "vite"
          }
        }
        """);
        CreatePackageJson(Path.Combine("web", "wwwroot", "libs", "bootstrap"), """
        {
          "scripts": {
            "dev": "vite"
          }
        }
        """);

        // Act
        var results = provider.GetRunnableNpmProjects(_testRootPath);

        // Assert
        results.Should().BeEmpty("generated dependency folders should not be scanned as runnable apps");
    }

    [Fact]
    public void GetRunnableApplications_ReturnsDotNetAndNpmApps()
    {
        // Arrange
        var provider = CreateProvider();
        CreateProjectWithProgramCs("MyProject.HttpApi.Host");
        CreatePackageJson("apps/angular", """
        {
          "scripts": {
            "start": "ng serve"
          }
        }
        """);

        // Act
        var results = provider.GetRunnableApplications(_testRootPath);

        // Assert
        results.Should().HaveCount(2);
        results.Should().ContainSingle(x => x.Type == RunnableAppType.DotNet && x.Name == "MyProject.HttpApi.Host.csproj");
        results.Should().ContainSingle(x => x.Type == RunnableAppType.Npm && x.Name == "angular:start");
    }

    #endregion

    #region GetRunnableProjectsWithMigrateDatabaseParameter Tests

    [Fact]
    public void GetRunnableProjectsWithMigrateDatabaseParameter_IdentifiesDbMigratorProjects()
    {
        // Arrange
        var provider = CreateProvider();

        // Create DbMigrator project with migrate-database parameter
        CreateDbMigratorProject("MyProject.DbMigrator");
        // Create regular project without migrate-database parameter
        CreateProjectWithProgramCs("MyProject.HttpApi.Host");

        // Act
        var results = provider.GetRunnableProjectsWithMigrateDatabaseParameter(_testRootPath);

        // Assert
        results.Should().HaveCount(1, "only DbMigrator project should be returned");
        results[0].FullName.Should().Contain("DbMigrator");
    }

    [Fact]
    public void GetRunnableProjectsWithMigrateDatabaseParameter_DetectsMigrateDatabaseInProgramCs()
    {
        // Arrange
        var provider = CreateProvider();
        var projectName = "MyProject.DbMigrator";
        var projectDir = CreateProjectDirectory(projectName);

        var programCsPath = Path.Combine(projectDir, "Program.cs");
        File.WriteAllText(programCsPath, @"
using System;

public class Program
{
    public static void Main(string[] args)
    {
        // Check for migrate-database parameter
        if (args.Contains(""--migrate-database""))
        {
            RunMigration();
        }
    }
}
");
        CreateProjectFile(Path.Combine(projectDir, $"{projectName}.csproj"));

        // Act
        var results = provider.GetRunnableProjectsWithMigrateDatabaseParameter(_testRootPath);

        // Assert
        results.Should().HaveCount(1);
        results[0].FullName.Should().Contain("DbMigrator");
    }

    [Fact]
    public void GetRunnableProjectsWithMigrateDatabaseParameter_DetectsMigrateDatabaseInModuleFile()
    {
        // Arrange
        var provider = CreateProvider();
        var projectName = "MyProject.DbMigrator";
        var projectDir = CreateProjectDirectory(projectName);

        // Create Program.cs without migrate-database
        var programCsPath = Path.Combine(projectDir, "Program.cs");
        File.WriteAllText(programCsPath, @"
using System;

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine(""Starting application..."");
    }
}
");

        // Create Module file with migrate-database
        var moduleCsPath = Path.Combine(projectDir, "MyProjectDbMigratorModule.cs");
        File.WriteAllText(moduleCsPath, @"
using Volo.Abp.Modularity;

public class MyProjectDbMigratorModule : AbpModule
{
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        // Supports --migrate-database parameter
    }
}
");

        CreateProjectFile(Path.Combine(projectDir, $"{projectName}.csproj"));

        // Act
        var results = provider.GetRunnableProjectsWithMigrateDatabaseParameter(_testRootPath);

        // Assert
        results.Should().HaveCount(1);
        results[0].FullName.Should().Contain("DbMigrator");
    }

    #endregion

    #region DoesHaveMigrateDatabaseParameter Tests

    [Fact]
    public void DoesHaveMigrateDatabaseParameter_WithNonExistentProject_ReturnsFalse()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var result = provider.DoesHaveMigrateDatabaseParameter("C:\\NonExistent\\Project.csproj");

        // Assert
        result.Should().BeFalse("non-existent project should not have migrate-database parameter");
    }

    [Fact]
    public void DoesHaveMigrateDatabaseParameter_WithMigrateDatabaseInProgramCs_ReturnsTrue()
    {
        // Arrange
        var provider = CreateProvider();
        var projectName = "TestProject";
        var projectDir = CreateProjectDirectory(projectName);
        var projectPath = Path.Combine(projectDir, $"{projectName}.csproj");

        var programCsPath = Path.Combine(projectDir, "Program.cs");
        File.WriteAllText(programCsPath, @"
public class Program
{
    public static void Main(string[] args)
    {
        if (args.Contains(""--migrate-database""))
        {
            Console.WriteLine(""Migrating database..."");
        }
    }
}
");
        CreateProjectFile(projectPath);

        // Act
        var result = provider.DoesHaveMigrateDatabaseParameter(projectPath);

        // Assert
        result.Should().BeTrue("project contains --migrate-database in Program.cs");
    }

    [Fact]
    public void DoesHaveMigrateDatabaseParameter_CaseInsensitive_MatchesParameter()
    {
        // Arrange
        var provider = CreateProvider();

        // Test with different case variations
        var testCases = new[]
        {
            "--migrate-database",
            "--MIGRATE-DATABASE",
            "--Migrate-Database",
            "--Migrate-DataBase"
        };

        foreach (var testCase in testCases)
        {
            var projectName = $"TestProject_{Guid.NewGuid()}";
            var projectDir = CreateProjectDirectory(projectName);
            var projectPath = Path.Combine(projectDir, $"{projectName}.csproj");

            var programCsPath = Path.Combine(projectDir, "Program.cs");
            File.WriteAllText(programCsPath, $@"
public class Program
{{
    public static void Main(string[] args)
    {{
        // Check for {testCase}
        if (args.Contains(""{testCase}""))
        {{
            Console.WriteLine(""Migrating..."");
        }}
    }}
}}
");
            CreateProjectFile(projectPath);

            // Act
            var result = provider.DoesHaveMigrateDatabaseParameter(projectPath);

            // Assert
            result.Should().BeTrue($"should match case-insensitive parameter: {testCase}");
        }
    }

    [Fact]
    public void DoesHaveMigrateDatabaseParameter_WithoutMigrateDatabaseParameter_ReturnsFalse()
    {
        // Arrange
        var provider = CreateProvider();
        var projectName = "TestProject";
        var projectDir = CreateProjectDirectory(projectName);
        var projectPath = Path.Combine(projectDir, $"{projectName}.csproj");

        var programCsPath = Path.Combine(projectDir, "Program.cs");
        File.WriteAllText(programCsPath, @"
public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine(""Starting application..."");
    }
}
");
        CreateProjectFile(projectPath);

        // Act
        var result = provider.DoesHaveMigrateDatabaseParameter(projectPath);

        // Assert
        result.Should().BeFalse("project does not contain --migrate-database parameter");
    }

    [Fact]
    public void DoesHaveMigrateDatabaseParameter_ChecksMultipleModuleFiles()
    {
        // Arrange
        var provider = CreateProvider();
        var projectName = "TestProject";
        var projectDir = CreateProjectDirectory(projectName);
        var projectPath = Path.Combine(projectDir, $"{projectName}.csproj");

        // Create Program.cs without migrate-database
        var programCsPath = Path.Combine(projectDir, "Program.cs");
        File.WriteAllText(programCsPath, @"
public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine(""Starting..."");
    }
}
");

        // Create first module without migrate-database
        var module1Path = Path.Combine(projectDir, "AppModule.cs");
        File.WriteAllText(module1Path, @"
// AppModule without migrate-database
public class AppModule { }
");

        // Create second module with migrate-database
        var module2Path = Path.Combine(projectDir, "ProjectModule.cs");
        File.WriteAllText(module2Path, @"
// ProjectModule with --migrate-database parameter support
public class ProjectModule { }
");

        CreateProjectFile(projectPath);

        // Act
        var result = provider.DoesHaveMigrateDatabaseParameter(projectPath);

        // Assert
        result.Should().BeTrue("should find --migrate-database in one of the module files");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a RunnableProjectsProvider with mocked configuration.
    /// </summary>
    private RunnableProjectsProvider CreateProvider()
    {
        // Mock RunConfiguration to avoid file operations
        var mockDeserializer = Substitute.For<YamlDotNet.Serialization.IDeserializer>();
        var mockSerializer = Substitute.For<YamlDotNet.Serialization.ISerializer>();

        var provider = new RunnableProjectsProvider(
            new RunConfiguration(mockDeserializer, mockSerializer));

        return provider;
    }

    /// <summary>
    /// Creates a project directory with the given name.
    /// </summary>
    private string CreateProjectDirectory(string projectName)
    {
        var projectDir = Path.Combine(_testRootPath, projectName);
        Directory.CreateDirectory(projectDir);
        return projectDir;
    }

    /// <summary>
    /// Creates a project file with the given path.
    /// </summary>
    private void CreateProjectFile(string projectPath)
    {
        var content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>";
        File.WriteAllText(projectPath, content);
    }

    /// <summary>
    /// Creates a project file with Program.cs in the same directory.
    /// </summary>
    private void CreateProjectFileWithProgramCs(string projectPath)
    {
        var directory = Path.GetDirectoryName(projectPath)!;
        var programCsPath = Path.Combine(directory, "Program.cs");
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
        CreateProjectFile(projectPath);
    }

    /// <summary>
    /// Creates a project file without Program.cs in the same directory.
    /// </summary>
    private void CreateProjectFileWithoutProgramCs(string projectPath)
    {
        CreateProjectFile(projectPath);
        // Intentionally don't create Program.cs
    }

    /// <summary>
    /// Creates a complete project with Program.cs file.
    /// </summary>
    private string CreateProjectWithProgramCs(string projectName)
    {
        var projectDir = CreateProjectDirectory(projectName);
        var projectPath = Path.Combine(projectDir, $"{projectName}.csproj");
        CreateProjectFileWithProgramCs(projectPath);
        return projectDir;
    }

    /// <summary>
    /// Creates a complete project without Program.cs file.
    /// </summary>
    private string CreateProjectWithoutProgramCs(string projectName)
    {
        var projectDir = CreateProjectDirectory(projectName);
        var projectPath = Path.Combine(projectDir, $"{projectName}.csproj");
        CreateProjectFileWithoutProgramCs(projectPath);
        return projectDir;
    }

    /// <summary>
    /// Creates a package.json file in a relative directory.
    /// </summary>
    private string CreatePackageJson(string relativeDirectory, string content)
    {
        var packageDirectory = Path.GetFullPath(Path.Combine(_testRootPath, relativeDirectory));
        Directory.CreateDirectory(packageDirectory);
        File.WriteAllText(Path.Combine(packageDirectory, "package.json"), content);
        return packageDirectory;
    }

    /// <summary>
    /// Creates a DbMigrator project with migrate-database parameter support.
    /// </summary>
    private string CreateDbMigratorProject(string projectName)
    {
        var projectDir = CreateProjectDirectory(projectName);

        // Create Program.cs with migrate-database parameter
        var programCsPath = Path.Combine(projectDir, "Program.cs");
        File.WriteAllText(programCsPath, @"
using System;

public class Program
{
    public static void Main(string[] args)
    {
        if (args.Contains(""--migrate-database""))
        {
            MigrateDatabase();
        }
    }

    private static void MigrateDatabase()
    {
        Console.WriteLine(""Migrating database..."");
    }
}
");

        CreateProjectFile(Path.Combine(projectDir, $"{projectName}.csproj"));
        return projectDir;
    }

    #endregion
}
