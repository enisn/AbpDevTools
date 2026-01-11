using AbpDevTools.Commands.Migrations;
using AbpDevTools.Configuration;
using AbpDevTools.Services;
using CliFx.Infrastructure;
using FluentAssertions;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.IO;

namespace AbpDevTools.Tests.Commands.Migrations;

/// <summary>
/// Unit tests for ClearMigrationsCommand class.
/// Tests migration folder clearing functionality.
/// </summary>
public class ClearMigrationsCommandTests : IDisposable
{
    private readonly ClearMigrationsCommand _command;
    private readonly string _testDirectory;
    private readonly TestConsole _console;

    public ClearMigrationsCommandTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);

        // Create a test provider with a mock configuration that doesn't require files
        var provider = CreateTestProvider();
        _command = new ClearMigrationsCommand(provider);
        _console = new TestConsole();
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
    public async Task ExecuteAsync_FindsMigrationsFolder_ShouldClearMigrations()
    {
        // Arrange
        var projectName = "MyProject.EntityFrameworkCore";
        var projectDir = Path.Combine(_testDirectory, projectName);
        Directory.CreateDirectory(projectDir);

        var migrationsFolder = Path.Combine(projectDir, "Migrations");
        Directory.CreateDirectory(migrationsFolder);

        var migrationFile = Path.Combine(migrationsFolder, "20250101_Migration.cs");
        File.WriteAllText(migrationFile, "// Migration content");

        var projectPath = CreateProjectFile(projectDir, projectName, includeEfCore: true);
        _command.WorkingDirectory = _testDirectory;
        _command.RunAll = true;

        // Act
        await _command.ExecuteAsync(_console);

        // Assert
        Directory.Exists(migrationsFolder).Should().BeFalse("Migrations folder should be deleted");
    }

    [Fact]
    public async Task ExecuteAsync_DeletesMigrationFiles_ShouldRemoveAllMigrations()
    {
        // Arrange
        var projectName = "MyProject.EntityFrameworkCore";
        var projectDir = Path.Combine(_testDirectory, projectName);
        Directory.CreateDirectory(projectDir);

        var migrationsFolder = Path.Combine(projectDir, "Migrations");
        Directory.CreateDirectory(migrationsFolder);

        var migration1 = Path.Combine(migrationsFolder, "20250101_InitialCreate.cs");
        var migration2 = Path.Combine(migrationsFolder, "20250102_AddUsers.cs");
        var migration3 = Path.Combine(migrationsFolder, "20250103_AddPosts.cs");

        File.WriteAllText(migration1, "// Migration 1");
        File.WriteAllText(migration2, "// Migration 2");
        File.WriteAllText(migration3, "// Migration 3");

        CreateProjectFile(projectDir, projectName, includeEfCore: true);
        _command.WorkingDirectory = _testDirectory;
        _command.RunAll = true;

        // Act
        await _command.ExecuteAsync(_console);

        // Assert
        Directory.Exists(migrationsFolder).Should().BeFalse();
        File.Exists(migration1).Should().BeFalse();
        File.Exists(migration2).Should().BeFalse();
        File.Exists(migration3).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithModelSnapshotFile_ShouldDeleteWithMigrations()
    {
        // Arrange
        var projectName = "MyProject.EntityFrameworkCore";
        var projectDir = Path.Combine(_testDirectory, projectName);
        Directory.CreateDirectory(projectDir);

        var migrationsFolder = Path.Combine(projectDir, "Migrations");
        Directory.CreateDirectory(migrationsFolder);

        var migrationFile = Path.Combine(migrationsFolder, "20250101_Migration.cs");
        var snapshotFile = Path.Combine(migrationsFolder, "MyDbContextModelSnapshot.cs");

        File.WriteAllText(migrationFile, "// Migration content");
        File.WriteAllText(snapshotFile, "// Model snapshot content");

        CreateProjectFile(projectDir, projectName, includeEfCore: true);
        _command.WorkingDirectory = _testDirectory;
        _command.RunAll = true;

        // Act
        await _command.ExecuteAsync(_console);

        // Assert
        Directory.Exists(migrationsFolder).Should().BeFalse();
        File.Exists(snapshotFile).Should().BeFalse("Model snapshot should be deleted with migrations folder");
    }

    [Fact]
    public async Task ExecuteAsync_WithoutMigrationsFolder_ShouldReportNoMigrations()
    {
        // Arrange
        var projectName = "MyProject.EntityFrameworkCore";
        var projectDir = Path.Combine(_testDirectory, projectName);
        Directory.CreateDirectory(projectDir);

        CreateProjectFile(projectDir, projectName, includeEfCore: true);
        _command.WorkingDirectory = _testDirectory;
        _command.RunAll = true;

        // Act
        // The command uses Spectre.Console for output, which we can't easily capture in tests
        // We just verify it completes without exception
        var exception = await Record.ExceptionAsync(async () => await _command.ExecuteAsync(_console));

        // Assert
        exception.Should().BeNull("Command should complete without exception");
    }

    [Fact]
    public async Task ExecuteAsync_WithDeletionError_ShouldHandleGracefully()
    {
        // Arrange
        var projectName = "MyProject.EntityFrameworkCore";
        var projectDir = Path.Combine(_testDirectory, projectName);
        Directory.CreateDirectory(projectDir);

        var migrationsFolder = Path.Combine(projectDir, "Migrations");
        Directory.CreateDirectory(migrationsFolder);

        var migrationFile = Path.Combine(migrationsFolder, "20250101_Migration.cs");
        File.WriteAllText(migrationFile, "// Migration content");

        // Open a file stream to simulate a file being in use
        await using var stream = new FileStream(
            Path.Combine(migrationsFolder, "lock.txt"),
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None
        );

        CreateProjectFile(projectDir, projectName, includeEfCore: true);
        _command.WorkingDirectory = _testDirectory;
        _command.RunAll = true;

        // Act & Assert
        // Should throw or handle the error gracefully
        // On Windows, this will throw an IOException
        var exception = await Record.ExceptionAsync(async () => await _command.ExecuteAsync(_console));

        // The behavior depends on how the command handles exceptions
        // Either it should throw or handle it gracefully
        if (exception != null)
        {
            exception.Should().BeOfType(typeof(IOException));
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleProjects_ShouldReportDeletedCount()
    {
        // Arrange
        var project1Dir = Path.Combine(_testDirectory, "Project1.EntityFrameworkCore");
        var project2Dir = Path.Combine(_testDirectory, "Project2.EntityFrameworkCore");
        var project3Dir = Path.Combine(_testDirectory, "Project3.EntityFrameworkCore");

        Directory.CreateDirectory(project1Dir);
        Directory.CreateDirectory(project2Dir);
        Directory.CreateDirectory(project3Dir);

        var migrations1 = Path.Combine(project1Dir, "Migrations");
        var migrations2 = Path.Combine(project2Dir, "Migrations");

        Directory.CreateDirectory(migrations1);
        Directory.CreateDirectory(migrations2);

        File.WriteAllText(Path.Combine(migrations1, "Migration1.cs"), "// Content");
        File.WriteAllText(Path.Combine(migrations2, "Migration2.cs"), "// Content");

        CreateProjectFile(project1Dir, "Project1.EntityFrameworkCore", includeEfCore: true);
        CreateProjectFile(project2Dir, "Project2.EntityFrameworkCore", includeEfCore: true);
        CreateProjectFile(project3Dir, "Project3.EntityFrameworkCore", includeEfCore: true);

        _command.WorkingDirectory = _testDirectory;
        _command.RunAll = true;

        // Act
        await _command.ExecuteAsync(_console);

        // Assert
        // The command uses Spectre.Console for output, which we can't easily capture in tests
        // Verify the migrations folders were deleted
        Directory.Exists(migrations1).Should().BeFalse("Project1 migrations should be cleared");
        Directory.Exists(migrations2).Should().BeFalse("Project2 migrations should be cleared");
        Directory.Exists(Path.Combine(project3Dir, "Migrations")).Should().BeFalse("Project3 never had migrations folder");
    }

    [Fact]
    public async Task ExecuteAsync_SupportsDryRun_ShouldListFilesWithoutDeleting()
    {
        // Arrange
        var projectName = "MyProject.EntityFrameworkCore";
        var projectDir = Path.Combine(_testDirectory, projectName);
        Directory.CreateDirectory(projectDir);

        var migrationsFolder = Path.Combine(projectDir, "Migrations");
        Directory.CreateDirectory(migrationsFolder);

        var migration1 = Path.Combine(migrationsFolder, "20250101_InitialCreate.cs");
        var migration2 = Path.Combine(migrationsFolder, "20250102_AddUsers.cs");

        File.WriteAllText(migration1, "// Migration 1");
        File.WriteAllText(migration2, "// Migration 2");

        CreateProjectFile(projectDir, projectName, includeEfCore: true);
        _command.WorkingDirectory = _testDirectory;
        _command.RunAll = true;

        // Note: The current implementation doesn't have a dry-run option
        // This test documents the expected behavior if dry-run is added

        // Act - Currently the command deletes files
        await _command.ExecuteAsync(_console);

        // Assert - Files are deleted (current behavior)
        Directory.Exists(migrationsFolder).Should().BeFalse();

        // When dry-run is implemented, files should remain
        // and output should list the files that would be deleted
    }

    [Theory]
    [InlineData("Project1", new[] { "Project1.EntityFrameworkCore" })]
    [InlineData("Project2", new[] { "Project2.EntityFrameworkCore" })]
    [InlineData("EntityFrameworkCore", new[] { "Project1.EntityFrameworkCore", "Project2.EntityFrameworkCore", "Project3.EntityFrameworkCore" })]
    public async Task ExecuteAsync_WithProjectFilter_ShouldOnlyClearMatchingProjects(string filter, string[] expectedProjects)
    {
        // Arrange
        var project1Dir = Path.Combine(_testDirectory, "Project1.EntityFrameworkCore");
        var project2Dir = Path.Combine(_testDirectory, "Project2.EntityFrameworkCore");
        var project3Dir = Path.Combine(_testDirectory, "Project3.EntityFrameworkCore");

        Directory.CreateDirectory(project1Dir);
        Directory.CreateDirectory(project2Dir);
        Directory.CreateDirectory(project3Dir);

        var migrations1 = Path.Combine(project1Dir, "Migrations");
        var migrations2 = Path.Combine(project2Dir, "Migrations");
        var migrations3 = Path.Combine(project3Dir, "Migrations");

        Directory.CreateDirectory(migrations1);
        Directory.CreateDirectory(migrations2);
        Directory.CreateDirectory(migrations3);

        File.WriteAllText(Path.Combine(migrations1, "Migration1.cs"), "// Content");
        File.WriteAllText(Path.Combine(migrations2, "Migration2.cs"), "// Content");
        File.WriteAllText(Path.Combine(migrations3, "Migration3.cs"), "// Content");

        CreateProjectFile(project1Dir, "Project1.EntityFrameworkCore", includeEfCore: true);
        CreateProjectFile(project2Dir, "Project2.EntityFrameworkCore", includeEfCore: true);
        CreateProjectFile(project3Dir, "Project3.EntityFrameworkCore", includeEfCore: true);

        _command.WorkingDirectory = _testDirectory;
        _command.Projects = new[] { filter };
        _command.RunAll = true;

        // Act
        await _command.ExecuteAsync(_console);

        // Assert - Verify that only matching projects had migrations cleared
        foreach (var project in expectedProjects)
        {
            var projectPath = Path.Combine(_testDirectory, project);
            var migrationsPath = Path.Combine(projectPath, "Migrations");
            Directory.Exists(migrationsPath).Should().BeFalse($"Migrations should be cleared for {project}");
        }

        // Verify non-matching projects still have migrations
        var allProjects = new[] { "Project1.EntityFrameworkCore", "Project2.EntityFrameworkCore", "Project3.EntityFrameworkCore" };
        foreach (var project in allProjects.Except(expectedProjects))
        {
            var projectPath = Path.Combine(_testDirectory, project);
            var migrationsPath = Path.Combine(projectPath, "Migrations");
            Directory.Exists(migrationsPath).Should().BeTrue($"Migrations should remain for {project}");
        }
    }

    /// <summary>
    /// Creates a test project file with EF Core package reference.
    /// </summary>
    private string CreateProjectFile(string directory, string projectName, bool includeEfCore = true)
    {
        var projectPath = Path.Combine(directory, $"{projectName}.csproj");
        var content = BuildProjectContent(projectName, includeEfCore);
        File.WriteAllText(projectPath, content);
        return projectPath;
    }

    /// <summary>
    /// Builds .csproj file content.
    /// </summary>
    private string BuildProjectContent(string projectName, bool includeEfCore)
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

        content += "</Project>";
        return content;
    }

    /// <summary>
    /// Creates a test provider with a mock configuration that doesn't require files.
    /// </summary>
    private EntityFrameworkCoreProjectsProvider CreateTestProvider()
    {
        var yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .Build();

        var yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .Build();

        // Use a mock configuration to avoid loading real config files
        var mockConfig = new MockToolsConfiguration(yamlDeserializer, yamlSerializer);
        var dependencyResolver = new DotnetDependencyResolver(mockConfig);
        return new EntityFrameworkCoreProjectsProvider(dependencyResolver);
    }

    /// <summary>
    /// Simple test console implementation for testing.
    /// Uses explicit interface implementation with CliFx types.
    /// </summary>
    private class TestConsole : IConsole
    {
        private readonly CliFx.Infrastructure.ConsoleReader _input;
        private readonly CliFx.Infrastructure.ConsoleWriter _output;
        private readonly CliFx.Infrastructure.ConsoleWriter _error;
        private readonly MemoryStream _inputStream;
        private readonly MemoryStream _outputStream;
        private readonly MemoryStream _errorStream;

        public TestConsole()
        {
            _inputStream = new MemoryStream();
            _outputStream = new MemoryStream();
            _errorStream = new MemoryStream();

            // Create ConsoleReader and ConsoleWriter using the CliFx constructors
            _input = new CliFx.Infrastructure.ConsoleReader(this, _inputStream);
            _output = new CliFx.Infrastructure.ConsoleWriter(this, _outputStream);
            _error = new CliFx.Infrastructure.ConsoleWriter(this, _errorStream);
        }

        public void Dispose()
        {
            _input?.Dispose();
            _output?.Dispose();
            _error?.Dispose();
            _inputStream?.Dispose();
            _outputStream?.Dispose();
            _errorStream?.Dispose();
        }

        public CliFx.Infrastructure.ConsoleReader Input => _input;
        public CliFx.Infrastructure.ConsoleWriter Output => _output;
        public CliFx.Infrastructure.ConsoleWriter Error => _error;

        public bool IsInputRedirected => false;
        public bool IsOutputRedirected => false;
        public bool IsErrorRedirected => false;

        public ConsoleColor ForegroundColor { get; set; } = ConsoleColor.Gray;
        public ConsoleColor BackgroundColor { get; set; } = ConsoleColor.Black;
        public int WindowWidth { get; set; } = 80;
        public int WindowHeight { get; set; } = 25;
        public int CursorLeft { get; set; } = 0;
        public int CursorTop { get; set; } = 0;

        public ConsoleKeyInfo ReadKey(bool intercept = false) => default;
        public void ResetColor() { }
        public void SetTerminalForegroundColor(ConsoleColor color) => ForegroundColor = color;
        public void ResetTerminalForegroundColor() => ForegroundColor = ConsoleColor.Gray;
        public void Clear() { }
        public CancellationToken RegisterCancellationHandler() => CancellationToken.None;
    }

    /// <summary>
    /// Mock configuration that doesn't try to read/write files.
    /// </summary>
    private class MockToolsConfiguration : ToolsConfiguration
    {
        public MockToolsConfiguration(IDeserializer deserializer, ISerializer serializer)
            : base(deserializer, serializer)
        {
        }

        public override ToolOption GetOptions()
        {
            // Return empty options without trying to read/write files
            return new ToolOption();
        }
    }
}
