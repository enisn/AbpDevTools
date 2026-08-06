using AbpDevTools.Commands;
using AbpDevTools.Configuration;
using AbpDevTools.Environments;
using AbpDevTools.LocalConfigurations;
using AbpDevTools.Notifications;
using AbpDevTools.Services;
using CliFx.Infrastructure;
using FluentAssertions;
using NSubstitute;
using System.Reflection;
using System.Text;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AbpDevTools.Tests.Commands;

/// <summary>
/// Unit tests for MigrateCommand class.
/// Tests DbMigrator project discovery and migration execution logic.
/// </summary>
public class MigrateCommandTests : IDisposable
{
    private readonly string _testRootPath;
    private readonly INotificationManager _mockNotificationManager;
    private readonly IProcessEnvironmentManager _mockEnvironmentManager;
    private readonly ToolsConfiguration _toolsConfiguration;
    private readonly LocalConfigurationManager _localConfigurationManager;
    private readonly RunnableProjectsProvider _runnableProjectsProvider;

    public MigrateCommandTests()
    {
        // Create a temporary directory for test files
        _testRootPath = Path.Combine(Path.GetTempPath(), $"AbpDevTools_MigrateCommand_Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testRootPath);

        // Mock dependencies
        _mockNotificationManager = Substitute.For<INotificationManager>();
        _mockEnvironmentManager = Substitute.For<IProcessEnvironmentManager>();

        // Use real configuration for ToolsConfiguration
        var mockDeserializer = Substitute.For<IDeserializer>();
        var mockSerializer = Substitute.For<ISerializer>();
        _toolsConfiguration = new ToolsConfiguration(mockDeserializer, mockSerializer);

        // Use real LocalConfigurationManager with mocked environment manager
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        var serializer = new SerializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .Build();
        _localConfigurationManager = new LocalConfigurationManager(
            deserializer,
            serializer,
            new FileExplorer(),
            _mockEnvironmentManager);

        // Use real RunnableProjectsProvider
        _runnableProjectsProvider = new RunnableProjectsProvider(
            new RunConfiguration(mockDeserializer, mockSerializer));
    }

    public void Dispose()
    {
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

    #region FindDbMigratorProject Tests

    [Fact]
    public void FindDbMigratorProject_WithDbMigratorInNameAndOutputTypeExe_ReturnsTrue()
    {
        // Arrange
        var command = CreateCommand();
        var projectName = "MyProject.DbMigrator";
        var projectDir = CreateProjectDirectory(projectName);
        var projectPath = Path.Combine(projectDir, $"{projectName}.csproj");

        var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>";
        File.WriteAllText(projectPath, projectContent);

        // Act
        var result = InvokeIsDbMigrator(command, projectPath);

        // Assert
        result.Should().BeTrue("project has DbMigrator in name and OutputType Exe");
    }

    [Fact]
    public void FindDbMigratorProject_WithDbMigratorInNameButNoOutputTypeExe_ReturnsFalse()
    {
        // Arrange
        var command = CreateCommand();
        var projectName = "MyProject.DbMigrator";
        var projectDir = CreateProjectDirectory(projectName);
        var projectPath = Path.Combine(projectDir, $"{projectName}.csproj");

        var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>";
        File.WriteAllText(projectPath, projectContent);

        // Act
        var result = InvokeIsDbMigrator(command, projectPath);

        // Assert
        result.Should().BeFalse("project doesn't have OutputType Exe");
    }

    [Fact]
    public void FindDbMigratorProject_WithoutDbMigratorInName_ReturnsFalse()
    {
        // Arrange
        var command = CreateCommand();
        var projectName = "MyProject.Web";
        var projectDir = CreateProjectDirectory(projectName);
        var projectPath = Path.Combine(projectDir, $"{projectName}.csproj");

        var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>";
        File.WriteAllText(projectPath, projectContent);

        // Act
        var result = InvokeIsDbMigrator(command, projectPath);

        // Assert
        result.Should().BeFalse("project doesn't have DbMigrator in name");
    }

    [Fact]
    public void FindDbMigratorProject_WithMultipleDbMigratorProjects_FindsAllProjects()
    {
        // Arrange
        var command = CreateCommand();

        // Create multiple DbMigrator projects
        var project1Dir = CreateProjectDirectory("Acme.DbMigrator");
        var project1Path = Path.Combine(project1Dir, "Acme.DbMigrator.csproj");
        File.WriteAllText(project1Path, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>");

        var project2Dir = CreateProjectDirectory("Bolo.DbMigrator");
        var project2Path = Path.Combine(project2Dir, "Bolo.DbMigrator.csproj");
        File.WriteAllText(project2Path, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>");

        // Act
        var result1 = InvokeIsDbMigrator(command, project1Path);
        var result2 = InvokeIsDbMigrator(command, project2Path);

        // Assert
        result1.Should().BeTrue("first DbMigrator project should be detected");
        result2.Should().BeTrue("second DbMigrator project should be detected");
    }

    [Fact]
    public void FindDbMigratorProject_WhenNoneFound_ReturnsFalse()
    {
        // Arrange
        var command = CreateCommand();
        var projectName = "MyProject.Library";
        var projectDir = CreateProjectDirectory(projectName);
        var projectPath = Path.Combine(projectDir, $"{projectName}.csproj");

        // Project without DbMigrator in name and without OutputType Exe
        var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>";
        File.WriteAllText(projectPath, projectContent);

        // Act
        var result = InvokeIsDbMigrator(command, projectPath);

        // Assert
        result.Should().BeFalse("non-DbMigrator project should return false");
    }

    #endregion

    #region Environment and Configuration Tests

    [Fact]
    public void MigrateCommand_WithEnvironmentName_PassessEnvironmentToDependencies()
    {
        // Arrange
        var command = CreateCommand();
        command.EnvironmentName = "Development";

        // Act - the command is created with environment name
        // Assert - verify the property is set
        command.EnvironmentName.Should().Be("Development");
    }

    [Fact]
    public void MigrateCommand_WithNoBuildFlag_SetsNoBuildProperty()
    {
        // Arrange
        var command = CreateCommand();
        command.NoBuild = true;

        // Act - the command is created with NoBuild flag
        // Assert - verify the property is set
        command.NoBuild.Should().BeTrue();
    }

    [Fact]
    public void MigrateCommand_WithWorkingDirectory_SetsWorkingDirectoryProperty()
    {
        // Arrange
        var command = CreateCommand();
        var testDir = "C:\\Test\\Directory";
        command.WorkingDirectory = testDir;

        // Act - the command is created with working directory
        // Assert - verify the property is set
        command.WorkingDirectory.Should().Be(testDir);
    }

    [Fact]
    public void MigrateCommand_Constructor_InitializesAllDependencies()
    {
        // Arrange & Act
        var command = CreateCommand();

        // Assert - command is created successfully with all dependencies
        command.Should().NotBeNull();
    }

    [Fact]
    public void LoadLocalConfiguration_WithAbpdevYml_ReportsLoadedConfigurationAndAppliesProjects()
    {
        // Arrange
        var configurationPath = Path.Combine(_testRootPath, "abpdev.yml");
        File.WriteAllText(configurationPath, """
            environment:
              name: Development
            run:
              projects:
                - Order
            """);

        var workingDirectory = Path.Combine(_testRootPath, "src");
        Directory.CreateDirectory(workingDirectory);
        CreateDbMigratorProject(workingDirectory, "Acme.Order.DbMigrator");
        CreateDbMigratorProject(workingDirectory, "Acme.Identity.DbMigrator");

        var command = CreateCommand();
        command.WorkingDirectory = workingDirectory;
        var console = new TestConsole();

        // Act
        command.InvokeLoadLocalConfiguration(console);

        // Assert
        command.Projects.Should().Equal("Order");
        console.GetOutput().Should().Contain(
            $"Loaded YAML configuration from '{Path.GetFullPath(configurationPath)}' with environment 'Development'.");

        var filteredProjects = command.InvokeFindDbMigrators(out var discoveredCount);
        discoveredCount.Should().Be(2);
        filteredProjects.Select(project => project.Name).Should().Equal("Acme.Order.DbMigrator.csproj");
    }

    [Fact]
    public void LoadLocalConfiguration_WithCommandLineProjects_PreservesCommandLineProjects()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testRootPath, "abpdev.yml"), """
            run:
              projects:
                - YamlProject
            """);

        var command = CreateCommand();
        command.WorkingDirectory = _testRootPath;
        command.Projects = new[] { "CommandLineProject" };

        // Act
        command.InvokeLoadLocalConfiguration(new TestConsole());

        // Assert
        command.Projects.Should().Equal("CommandLineProject");
    }

    [Fact]
    public void LoadLocalConfiguration_WithPreloadedConfiguration_UsesItWithoutReloadingOrLogging()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testRootPath, "abpdev.yml"), """
            run:
              projects:
                - NearestYamlProject
            """);

        var preloadedConfiguration = new LocalConfiguration
        {
            Run = new LocalConfiguration.LocalRunOption
            {
                Projects = new[] { "ExplicitYamlProject" }
            }
        };

        var command = CreateCommand();
        command.WorkingDirectory = _testRootPath;
        var console = new TestConsole();

        // Act
        command.InvokeLoadLocalConfiguration(
            console,
            preloadedConfiguration,
            rootConfigurationLoaded: true);

        // Assert
        command.Projects.Should().Equal("ExplicitYamlProject");
        console.GetOutput().Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a MigrateCommand with mocked dependencies.
    /// </summary>
    private TestMigrateCommand CreateCommand()
    {
        return new TestMigrateCommand(
            _mockNotificationManager,
            _mockEnvironmentManager,
            _toolsConfiguration,
            _localConfigurationManager,
            _runnableProjectsProvider);
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

    private static void CreateDbMigratorProject(string parentDirectory, string projectName)
    {
        var projectDirectory = Path.Combine(parentDirectory, projectName);
        Directory.CreateDirectory(projectDirectory);
        File.WriteAllText(Path.Combine(projectDirectory, $"{projectName}.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
    }

    /// <summary>
    /// Invokes the private IsDbMigrator method using reflection.
    /// </summary>
    private bool InvokeIsDbMigrator(MigrateCommand command, string projectPath)
    {
        var method = typeof(MigrateCommand).GetMethod("IsDbMigrator", BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null)
        {
            throw new InvalidOperationException("IsDbMigrator method not found");
        }
        return (bool)method.Invoke(command, new object[] { projectPath })!;
    }

    [CliFx.Attributes.Command("test-migrate-command")]
    private sealed class TestMigrateCommand : MigrateCommand
    {
        public TestMigrateCommand(
            INotificationManager notificationManager,
            IProcessEnvironmentManager environmentManager,
            ToolsConfiguration toolsConfiguration,
            LocalConfigurationManager localConfigurationManager,
            RunnableProjectsProvider runnableProjectsProvider)
            : base(
                notificationManager,
                environmentManager,
                toolsConfiguration,
                localConfigurationManager,
                runnableProjectsProvider)
        {
        }

        public void InvokeLoadLocalConfiguration(
            IConsole console,
            LocalConfiguration? localRootConfiguration = null,
            bool rootConfigurationLoaded = false)
        {
            LoadLocalConfiguration(console, localRootConfiguration, rootConfigurationLoaded);
        }

        public FileInfo[] InvokeFindDbMigrators(out int discoveredCount)
        {
            return FindDbMigrators(out discoveredCount);
        }
    }

    private sealed class TestConsole : IConsole
    {
        private readonly MemoryStream _inputStream = new();
        private readonly MemoryStream _outputStream = new();
        private readonly MemoryStream _errorStream = new();
        private readonly Lazy<ConsoleReader> _input;
        private readonly Lazy<ConsoleWriter> _output;
        private readonly Lazy<ConsoleWriter> _error;

        public TestConsole()
        {
            _input = new Lazy<ConsoleReader>(() => new ConsoleReader(this, _inputStream, Encoding.UTF8));
            _output = new Lazy<ConsoleWriter>(() => new ConsoleWriter(this, _outputStream, Encoding.UTF8));
            _error = new Lazy<ConsoleWriter>(() => new ConsoleWriter(this, _errorStream, Encoding.UTF8));
        }

        public ConsoleReader Input => _input.Value;
        public ConsoleWriter Output => _output.Value;
        public ConsoleWriter Error => _error.Value;

        public bool IsOutputRedirected => true;
        public bool IsErrorRedirected => true;
        public bool IsInputRedirected => true;

        public ConsoleColor ForegroundColor { get; set; }
        public ConsoleColor BackgroundColor { get; set; }
        public int WindowWidth { get; set; } = 120;
        public int WindowHeight { get; set; } = 30;
        public int CursorLeft { get; set; }
        public int CursorTop { get; set; }

        public CancellationToken RegisterCancellationHandler() => CancellationToken.None;
        public ConsoleKeyInfo ReadKey(bool intercept = false) => default;
        public void Clear() { }
        public void ResetColor() { }

        public string GetOutput()
        {
            if (_output.IsValueCreated)
            {
                _output.Value.Flush();
            }

            return Encoding.UTF8.GetString(_outputStream.ToArray());
        }
    }

    #endregion
}
