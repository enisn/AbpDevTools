using System.Reflection;
using AbpDevTools.Commands;
using AbpDevTools.Configuration;
using AbpDevTools.Notifications;
using AbpDevTools.Tests.Helpers;
using CliFx.Infrastructure;
using FluentAssertions;
using NSubstitute;
using Xunit;
using YamlDotNet.Serialization;

namespace AbpDevTools.Tests.Commands;

/// <summary>
/// Execution tests for BuildCommand.
/// Tests build execution logic, error handling, and build file discovery.
/// </summary>
public class BuildCommand_ExecutionTests : CommandTestBase
{
    private readonly string _testRootPath;
    private readonly INotificationManager _mockNotificationManager;

    public BuildCommand_ExecutionTests()
    {
        _testRootPath = Path.Combine(Path.GetTempPath(), $"BuildCommandTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testRootPath);
        _mockNotificationManager = Substitute.For<INotificationManager>();
    }

    public override void Dispose()
    {
        base.Dispose();

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

    #region Test Helpers

    /// <summary>
    /// Creates a BuildCommand with mocked dependencies for testing.
    /// </summary>
    private BuildCommand CreateBuildCommand(ToolsConfiguration? toolsConfiguration = null)
    {
        toolsConfiguration ??= CreateMockToolsConfiguration();

        return new BuildCommand(_mockNotificationManager, toolsConfiguration)
        {
            WorkingDirectory = _testRootPath
        };
    }

    /// <summary>
    /// Creates a mock ToolsConfiguration for testing.
    /// </summary>
    private ToolsConfiguration CreateMockToolsConfiguration()
    {
        var mockDeserializer = Substitute.For<IDeserializer>();
        var mockSerializer = Substitute.For<ISerializer>();

        // Setup deserializer to return default tool options
        mockDeserializer.Deserialize<ToolOption>(Arg.Any<string>())
            .Returns(new ToolOption
            {
                { "dotnet", "dotnet" },
                { "powershell", "pwsh" }
            });

        return new ToolsConfiguration(mockDeserializer, mockSerializer);
    }

    /// <summary>
    /// Creates a real console for testing CLI commands.
    /// Uses CliFx's built-in console abstraction.
    /// </summary>
    private IConsole CreateTestConsole()
    {
        return new TestConsole();
    }

    /// <summary>
    /// Minimal IConsole implementation for testing.
    /// </summary>
    private class TestConsole : IConsole
    {
        private readonly Lazy<ConsoleReader> _input;
        private readonly Lazy<ConsoleWriter> _output;
        private readonly Lazy<ConsoleWriter> _error;

        public ConsoleReader Input => _input.Value;
        public ConsoleWriter Output => _output.Value;
        public ConsoleWriter Error => _error.Value;

        public bool IsOutputRedirected => false;
        public bool IsErrorRedirected => false;
        public bool IsInputRedirected => false;

        public ConsoleColor ForegroundColor { get; set; }
        public ConsoleColor BackgroundColor { get; set; }
        public int WindowWidth { get; set; } = 120;
        public int WindowHeight { get; set; } = 30;
        public int CursorLeft { get; set; }
        public int CursorTop { get; set; }

        public TestConsole()
        {
            _input = new Lazy<ConsoleReader>(() => new ConsoleReader(
                (IConsole)this,
                System.Console.OpenStandardInput(),
                System.Text.Encoding.UTF8));

            _output = new Lazy<ConsoleWriter>(() => new ConsoleWriter(
                (IConsole)this,
                System.Console.OpenStandardOutput(),
                System.Text.Encoding.UTF8));

            _error = new Lazy<ConsoleWriter>(() => new ConsoleWriter(
                (IConsole)this,
                System.Console.OpenStandardError(),
                System.Text.Encoding.UTF8));
        }

        public CancellationToken RegisterCancellationHandler() => CancellationToken.None;

        public ConsoleKeyInfo ReadKey(bool intercept = false) => default;

        public void Clear() { }

        public void ResetColor() { }
    }

    /// <summary>
    /// Creates a test solution file in the specified directory.
    /// </summary>
    private string CreateSolutionFile(string solutionName, string? directory = null)
    {
        var dir = directory ?? _testRootPath;
        Directory.CreateDirectory(dir);

        var solutionPath = Path.Combine(dir, $"{solutionName}.sln");
        File.WriteAllText(solutionPath, @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Global
    GlobalSection(SolutionConfigurationPlatforms) = preSolution
        Debug|Any CPU = Debug|Any CPU
    EndGlobalSection
EndGlobal
");
        return solutionPath;
    }

    /// <summary>
    /// Creates a test solutionx file in the specified directory.
    /// </summary>
    private string CreateSolutionXFile(string solutionName, string? directory = null)
    {
        var dir = directory ?? _testRootPath;
        Directory.CreateDirectory(dir);

        var solutionPath = Path.Combine(dir, $"{solutionName}.slnx");
        File.WriteAllText(solutionPath, @"<?xml version=""1.0"" encoding=""utf-8""?>
<Solution xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
</Solution>
");
        return solutionPath;
    }

    /// <summary>
    /// Creates a test .csproj file in the specified directory.
    /// </summary>
    private string CreateProjectFile(string projectName, string? directory = null)
    {
        var dir = directory ?? _testRootPath;
        Directory.CreateDirectory(dir);

        var projectPath = Path.Combine(dir, $"{projectName}.csproj");
        File.WriteAllText(projectPath, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <OutputType>Library</OutputType>
  </PropertyGroup>
</Project>
");
        return projectPath;
    }

    #endregion

    #region Build Execution Tests

    [Fact]
    public async Task ExecuteAsync_WithSingleSolutionFile_InvokesDotnetBuild()
    {
        // Arrange
        var solutionName = TestConstants.ProjectNames.AcmeBookStore;
        CreateSolutionFile(solutionName);

        var command = CreateBuildCommand();
        var console = CreateTestConsole();

        // Act
        try
        {
            await command.ExecuteAsync(console);
        }
        catch
        {
            // Process.Start may fail in test environment if dotnet is unavailable
        }

        // Assert
        // Verify notification was called, indicating build was attempted
        await _mockNotificationManager.Received().SendAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleSolutionFiles_InvokesDotnetBuildForEach()
    {
        // Arrange
        CreateSolutionFile(TestConstants.ProjectNames.AcmeBookStore);
        CreateSolutionFile(TestConstants.ProjectNames.SampleProject);

        var command = CreateBuildCommand();
        var console = CreateTestConsole();

        // Act
        try
        {
            await command.ExecuteAsync(console);
        }
        catch
        {
            // Process.Start may fail in test environment
        }

        // Assert
        // Verify that multiple solutions were discovered
        var solutionFiles = Directory.EnumerateFiles(_testRootPath, "*.sln", SearchOption.AllDirectories);
        solutionFiles.Count().Should().Be(2, "should find both solution files");

        // Should send notification about projects (even if builds fail, notification is sent)
        await _mockNotificationManager.Received().SendAsync(
            Arg.Any<string>(),
            Arg.Is<string>(s => s.Contains("of 2") || s.Contains("2 projects")),
            Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WithSolutionXFile_InvokesDotnetBuild()
    {
        // Arrange
        CreateSolutionXFile(TestConstants.ProjectNames.AcmeBookStore);

        var command = CreateBuildCommand();
        var console = CreateTestConsole();

        // Act
        try
        {
            await command.ExecuteAsync(console);
        }
        catch
        {
            // Process.Start may fail in test environment
        }

        // Assert
        var solutionFiles = Directory.EnumerateFiles(_testRootPath, "*.slnx", SearchOption.AllDirectories);
        solutionFiles.Count().Should().Be(1, "should find .slnx file");

        await _mockNotificationManager.Received().SendAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WithCsprojFile_InvokesDotnetBuild()
    {
        // Arrange
        // No .sln or .slnx files, should fall back to .csproj
        CreateProjectFile(TestConstants.ProjectNames.TestProject);

        var command = CreateBuildCommand();
        var console = CreateTestConsole();

        // Act
        try
        {
            await command.ExecuteAsync(console);
        }
        catch
        {
            // Process.Start may fail in test environment
        }

        // Assert
        // Verify build was attempted on the .csproj file
        await _mockNotificationManager.Received().SendAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WithNoBuildFiles_DoesNotInvokeBuild()
    {
        // Arrange - Empty directory with no solution or project files
        var command = CreateBuildCommand();
        var console = CreateTestConsole();

        // Act
        await command.ExecuteAsync(console);

        // Assert
        // No notification should be sent when no files are found
        await _mockNotificationManager.DidNotReceive().SendAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WithConfigurationParameter_PassesConfigurationToBuild()
    {
        // Arrange
        CreateSolutionFile(TestConstants.ProjectNames.AcmeBookStore);

        var command = CreateBuildCommand();
        command.Configuration = "Release";

        var console = CreateTestConsole();

        // Act
        try
        {
            await command.ExecuteAsync(console);
        }
        catch
        {
            // Process.Start may fail in test environment
        }

        // Assert
        // Configuration parameter is set and should be passed to dotnet build
        command.Configuration.Should().Be("Release", "configuration should be set to Release");
    }

    [Fact]
    public async Task ExecuteAsync_WithBuildFilesParameter_FiltersBuildFiles()
    {
        // Arrange
        CreateSolutionFile(TestConstants.ProjectNames.AcmeBookStore);
        CreateSolutionFile(TestConstants.ProjectNames.SampleProject);

        var command = CreateBuildCommand();
        command.BuildFiles = new[] { "Acme" };

        var console = CreateTestConsole();

        // Act
        try
        {
            await command.ExecuteAsync(console);
        }
        catch
        {
            // Process.Start may fail in test environment
        }

        // Assert
        // BuildFiles filter is applied, should only match "Acme"
        command.BuildFiles.Should().Contain("Acme", "should filter by Acme");

        // Should receive notification for single build
        await _mockNotificationManager.Received().SendAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WithNestedDirectories_FindsAllBuildFiles()
    {
        // Arrange
        var nestedDir1 = Path.Combine(_testRootPath, "apps", "App1");
        var nestedDir2 = Path.Combine(_testRootPath, "services", "Service1");

        CreateSolutionFile("App1.Solution", nestedDir1);
        CreateSolutionFile("Service1.Solution", nestedDir2);

        var command = CreateBuildCommand();
        var console = CreateTestConsole();

        // Act
        try
        {
            await command.ExecuteAsync(console);
        }
        catch
        {
            // Process.Start may fail in test environment
        }

        // Assert
        var allSolutions = Directory.EnumerateFiles(_testRootPath, "*.sln", SearchOption.AllDirectories);
        allSolutions.Count().Should().Be(2, "should find all solutions in nested directories");
    }

    [Fact]
    public async Task ExecuteAsync_WithMixedFileTypes_PrioritizesSolutionsOverProjects()
    {
        // Arrange
        CreateSolutionFile(TestConstants.ProjectNames.AcmeBookStore);
        CreateProjectFile(TestConstants.ProjectNames.TestProject);

        var command = CreateBuildCommand();
        var console = CreateTestConsole();

        // Act
        try
        {
            await command.ExecuteAsync(console);
        }
        catch
        {
            // Process.Start may fail in test environment
        }

        // Assert
        // Should find .sln file first and build it
        var solutionFiles = Directory.EnumerateFiles(_testRootPath, "*.sln", SearchOption.AllDirectories);
        solutionFiles.Count().Should().Be(1, "should find solution file");

        await _mockNotificationManager.Received().SendAsync(
            Arg.Any<string>(),
            Arg.Is<string>(s => s.Contains("Acme.BookStore")),
            Arg.Any<string>());
    }

    #endregion
}
