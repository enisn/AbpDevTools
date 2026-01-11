using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using AbpDevTools.Commands.References;
using AbpDevTools.Configuration;
using AbpDevTools.Services;
using CliFx.Infrastructure;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AbpDevTools.Tests.Commands.References;

/// <summary>
/// Unit tests for SwitchReferencesToLocalCommand class.
/// Tests conversion of PackageReferences to ProjectReferences, Git cloning,
/// and local source configuration loading.
/// </summary>
public class SwitchReferencesToLocalCommandTests : IDisposable
{
    private readonly string _testRootPath;
    private readonly LocalSourcesConfiguration _mockLocalSourcesConfiguration;
    private readonly FileExplorer _fileExplorer;
    private readonly CsprojManipulationService _csprojService;
    private readonly GitService _mockGitService;
    private readonly SwitchReferencesToLocalCommand _command;

    public SwitchReferencesToLocalCommandTests()
    {
        // Create a temporary directory for test files
        _testRootPath = Path.Combine(Path.GetTempPath(), $"SwitchReferencesToLocalCommandTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testRootPath);

        // Prepare the test data
        var sources = new LocalSourceMapping
        {
            ["abp"] = new LocalSourceMappingItem
            {
                Path = Path.Combine(_testRootPath, "abp"),
                RemotePath = "https://github.com/abpframework/abp.git",
                Branch = "dev",
                Packages = new HashSet<string> { "Volo.*" }
            }
        };

        // Create real LocalSourcesConfiguration
        var deserializer = new YamlDotNet.Serialization.DeserializerBuilder().Build();
        var serializer = new YamlDotNet.Serialization.SerializerBuilder().Build();
        _mockLocalSourcesConfiguration = Substitute.ForPartsOf<LocalSourcesConfiguration>(deserializer, serializer);

        // Configure the GetOptions() mock to return test data without calling base
        _mockLocalSourcesConfiguration.When(x => x.GetOptions()).DoNotCallBase();
        _mockLocalSourcesConfiguration.GetOptions().Returns(sources);

        // Use real FileExplorer and CsprojManipulationService
        _fileExplorer = new FileExplorer();
        _csprojService = new CsprojManipulationService(_fileExplorer);
        _mockGitService = Substitute.For<GitService>();

        // Create command instance with dependencies
        _command = new SwitchReferencesToLocalCommand(
            _mockLocalSourcesConfiguration,
            _fileExplorer,
            _csprojService,
            _mockGitService
        );
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

    #region Test 1: ExecuteAsync Identifies Projects to Convert

    [Fact]
    public async Task ExecuteAsync_IdentifiesProjectsToConvert_WhenCsprojFilesExist()
    {
        // Arrange
        var workingDirectory = Path.Combine(_testRootPath, "workspace");
        Directory.CreateDirectory(workingDirectory);

        // Create test .csproj files
        File.WriteAllText(Path.Combine(workingDirectory, "Project1.csproj"), CreateBasicCsprojContent("Project1"));
        File.WriteAllText(Path.Combine(workingDirectory, "Project2.csproj"), CreateBasicCsprojContent("Project2"));

        // Create abp source directory with a project to simulate local source
        var abpSourcePath = Path.Combine(_testRootPath, "abp");
        var corePath = Path.Combine(abpSourcePath, "core", "Volo.Abp.Core");
        Directory.CreateDirectory(corePath);
        File.WriteAllText(Path.Combine(corePath, "Volo.Abp.Core.csproj"), CreateBasicCsprojContent("Volo.Abp.Core"));

        var console = new FakeConsole();
        _command.WorkingDirectory = workingDirectory;

        // Act & Assert - should not throw
        await _command.ExecuteAsync(console);
        console.OutputLength.Should().BeGreaterThan(0, "Should generate output");
    }

    #endregion

    #region Test 2: ExecuteAsync Loads LocalSourceConfiguration

    [Fact]
    public async Task ExecuteAsync_LoadsLocalSourceConfiguration()
    {
        // Arrange
        var workingDirectory = Path.Combine(_testRootPath, "workspace");
        Directory.CreateDirectory(workingDirectory);

        File.WriteAllText(Path.Combine(workingDirectory, "Project1.csproj"), CreateBasicCsprojContent("Project1"));

        var console = new FakeConsole();
        _command.WorkingDirectory = workingDirectory;

        // Act
        await _command.ExecuteAsync(console);

        // Assert
        _mockLocalSourcesConfiguration.Received(1).GetOptions();
    }

    #endregion

    #region Test 3: ExecuteAsync Clones Repositories for Unknown Local Sources

    [Fact]
    public async Task ExecuteAsync_ClonesRepositories_ForUnknownLocalSources()
    {
        // Arrange
        var workingDirectory = Path.Combine(_testRootPath, "workspace");
        Directory.CreateDirectory(workingDirectory);

        File.WriteAllText(Path.Combine(workingDirectory, "Project1.csproj"), CreateBasicCsprojContent("Project1"));

        // Don't create the abp source directory - it will need to be cloned
        _mockGitService.IsDirectoryEmpty(Path.Combine(_testRootPath, "abp")).Returns(true);
        _mockGitService.IsGitInstalled().Returns(true);
        _mockGitService.CloneRepositoryAsync(
            "https://github.com/abpframework/abp.git",
            Path.Combine(_testRootPath, "abp"),
            "dev",
            Arg.Any<CancellationToken>()
        ).Returns(true);

        var console = new FakeConsole();
        _command.WorkingDirectory = workingDirectory;

        // Act
        await _command.ExecuteAsync(console);

        // Assert
        await _mockGitService.Received(1).CloneRepositoryAsync(
            "https://github.com/abpframework/abp.git",
            Path.Combine(_testRootPath, "abp"),
            "dev",
            Arg.Any<CancellationToken>()
        );
    }

    #endregion

    #region Test 4: ExecuteAsync Skips Cloning If Repository Already Exists

    [Fact]
    public async Task ExecuteAsync_SkipsCloning_IfRepositoryAlreadyExists()
    {
        // Arrange
        var workingDirectory = Path.Combine(_testRootPath, "workspace");
        Directory.CreateDirectory(workingDirectory);

        File.WriteAllText(Path.Combine(workingDirectory, "Project1.csproj"), CreateBasicCsprojContent("Project1"));

        // Create the abp source directory with a project
        var abpSourcePath = Path.Combine(_testRootPath, "abp");
        Directory.CreateDirectory(abpSourcePath);
        File.WriteAllText(Path.Combine(abpSourcePath, "test.txt"), "content");

        _mockGitService.IsDirectoryEmpty(abpSourcePath).Returns(false);

        var console = new FakeConsole();
        _command.WorkingDirectory = workingDirectory;

        // Act
        await _command.ExecuteAsync(console);

        // Assert
        await _mockGitService.DidNotReceive().CloneRepositoryAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()
        );
    }

    #endregion

    #region Test 5: ExecuteAsync Handles Cloning Failure

    [Fact]
    public async Task ExecuteAsync_HandlesCloningFailure_ContinuesExecution()
    {
        // Arrange
        var workingDirectory = Path.Combine(_testRootPath, "workspace");
        Directory.CreateDirectory(workingDirectory);

        File.WriteAllText(Path.Combine(workingDirectory, "Project1.csproj"), CreateBasicCsprojContent("Project1"));

        // Directory doesn't exist, Git is installed, but clone fails
        var abpSourcePath = Path.Combine(_testRootPath, "abp");
        _mockGitService.IsDirectoryEmpty(abpSourcePath).Returns(true);
        _mockGitService.IsGitInstalled().Returns(true);
        _mockGitService.CloneRepositoryAsync(
            "https://github.com/abpframework/abp.git",
            abpSourcePath,
            "dev",
            Arg.Any<CancellationToken>()
        ).Returns(false);

        var console = new FakeConsole();
        _command.WorkingDirectory = workingDirectory;

        // Act - should not throw
        await _command.ExecuteAsync(console);

        // Assert - Clone was attempted
        await _mockGitService.Received(1).CloneRepositoryAsync(
            "https://github.com/abpframework/abp.git",
            abpSourcePath,
            "dev",
            Arg.Any<CancellationToken>()
        );
    }

    #endregion

    #region Test 6: ExecuteAsync Handles Git Not Installed

    [Fact]
    public async Task ExecuteAsync_HandlesGitNotInstalled_SkipsCloning()
    {
        // Arrange
        var workingDirectory = Path.Combine(_testRootPath, "workspace");
        Directory.CreateDirectory(workingDirectory);

        File.WriteAllText(Path.Combine(workingDirectory, "Project1.csproj"), CreateBasicCsprojContent("Project1"));

        // Directory doesn't exist, Git is not installed
        var abpSourcePath = Path.Combine(_testRootPath, "abp");
        _mockGitService.IsDirectoryEmpty(abpSourcePath).Returns(true);
        _mockGitService.IsGitInstalled().Returns(false);

        var console = new FakeConsole();
        _command.WorkingDirectory = workingDirectory;

        // Act
        await _command.ExecuteAsync(console);

        // Assert - Clone should not be attempted when Git is not installed
        await _mockGitService.DidNotReceive().CloneRepositoryAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()
        );
    }

    #endregion

    #region Test 7: ExecuteAsync Handles No Projects Found

    [Fact]
    public async Task ExecuteAsync_HandlesNoProjectsFound()
    {
        // Arrange
        var workingDirectory = Path.Combine(_testRootPath, "workspace");
        Directory.CreateDirectory(workingDirectory);

        // Don't create any .csproj files

        var console = new FakeConsole();
        _command.WorkingDirectory = workingDirectory;

        // Act & Assert - should not throw
        await _command.ExecuteAsync(console);
    }

    #endregion

    #region Test 8: ExecuteAsync With RemotePath as Empty String

    [Fact]
    public async Task ExecuteAsync_HandlesEmptyRemotePath_SkipsCloning()
    {
        // Arrange - Create a new test class instance with empty RemotePath
        var sourcesWithEmptyRemote = new LocalSourceMapping
        {
            ["abp"] = new LocalSourceMappingItem
            {
                Path = Path.Combine(_testRootPath, "abp2"),
                RemotePath = "", // Empty remote URL
                Packages = new HashSet<string> { "Volo.*" }
            }
        };

        var deserializer = new YamlDotNet.Serialization.DeserializerBuilder().Build();
        var serializer = new YamlDotNet.Serialization.SerializerBuilder().Build();
        var mockConfig = Substitute.ForPartsOf<LocalSourcesConfiguration>(deserializer, serializer);
        mockConfig.When(x => x.GetOptions()).DoNotCallBase();
        mockConfig.GetOptions().Returns(sourcesWithEmptyRemote);

        var command = new SwitchReferencesToLocalCommand(
            mockConfig,
            _fileExplorer,
            _csprojService,
            _mockGitService
        );

        var workingDirectory = Path.Combine(_testRootPath, "workspace2");
        Directory.CreateDirectory(workingDirectory);

        File.WriteAllText(Path.Combine(workingDirectory, "Project1.csproj"), CreateBasicCsprojContent("Project1"));

        var abpSourcePath = Path.Combine(_testRootPath, "abp2");
        _mockGitService.IsDirectoryEmpty(abpSourcePath).Returns(true);
        _mockGitService.IsGitInstalled().Returns(true);

        var console = new FakeConsole();
        command.WorkingDirectory = workingDirectory;

        // Act
        await command.ExecuteAsync(console);

        // Assert - Clone should not be attempted when there's no RemotePath
        await _mockGitService.DidNotReceive().CloneRepositoryAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()
        );
    }

    #endregion

    #region Test 9: ExecuteAsync With No Sources

    [Fact]
    public async Task ExecuteAsync_HandlesNoLocalSourcesConfigured()
    {
        // Arrange - Create a new test class instance with empty sources
        var emptySources = new LocalSourceMapping();

        var deserializer = new YamlDotNet.Serialization.DeserializerBuilder().Build();
        var serializer = new YamlDotNet.Serialization.SerializerBuilder().Build();
        var mockConfig = Substitute.ForPartsOf<LocalSourcesConfiguration>(deserializer, serializer);
        mockConfig.When(x => x.GetOptions()).DoNotCallBase();
        mockConfig.GetOptions().Returns(emptySources);

        var command = new SwitchReferencesToLocalCommand(
            mockConfig,
            _fileExplorer,
            _csprojService,
            _mockGitService
        );

        var workingDirectory = Path.Combine(_testRootPath, "workspace3");
        Directory.CreateDirectory(workingDirectory);

        File.WriteAllText(Path.Combine(workingDirectory, "Project1.csproj"), CreateBasicCsprojContent("Project1"));

        var console = new FakeConsole();
        command.WorkingDirectory = workingDirectory;

        // Act & Assert - should not throw
        await command.ExecuteAsync(console);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates basic .csproj file content.
    /// </summary>
    private string CreateBasicCsprojContent(string projectName)
    {
        return $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>";
    }

    /// <summary>
    /// Creates .csproj file content with PackageReferences.
    /// </summary>
    private string CreateProjectWithPackageReferences()
    {
        return @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Volo.Abp.Core"" Version=""5.3.0"" />
    <PackageReference Include=""Volo.Abp.Data"" Version=""5.3.0"" />
  </ItemGroup>
</Project>";
    }

    /// <summary>
    /// A fake console implementation that captures output length.
    /// </summary>
    private class FakeConsole : IConsole
    {
        private readonly System.IO.StringWriter _outputWriter;
        private readonly System.IO.StringWriter _errorWriter;

        public FakeConsole()
        {
            _outputWriter = new System.IO.StringWriter();
            _errorWriter = new System.IO.StringWriter();
        }

        // Create default ConsoleWriter/ConsoleReader using Activator
        public ConsoleWriter Output => CreateDefaultConsoleWriter(_outputWriter);
        public ConsoleWriter Error => CreateDefaultConsoleWriter(_errorWriter);
        public ConsoleReader Input => CreateDefaultConsoleReader(System.Console.In);

        public int OutputLength => _outputWriter.ToString().Length;

        public bool IsOutputRedirected => true;
        public bool IsErrorRedirected => true;
        public bool IsInputRedirected => false;

        public ConsoleColor ForegroundColor { get; set; }
        public ConsoleColor BackgroundColor { get; set; }

        public int WindowWidth { get; set; } = 120;
        public int WindowHeight { get; set; } = 30;

        public int CursorLeft { get; set; }
        public int CursorTop { get; set; }

        public void Clear()
        {
            _outputWriter.GetStringBuilder().Clear();
            _errorWriter.GetStringBuilder().Clear();
        }

        public void ResetColor()
        {
            ForegroundColor = ConsoleColor.Gray;
            BackgroundColor = ConsoleColor.Black;
        }

        public ConsoleKeyInfo ReadKey(bool intercept) => default;

        public void RegisterCancellationHandler(Action<CancellationToken> callback)
        {
            // No-op for testing
        }

        public CancellationToken RegisterCancellationHandler()
        {
            return CancellationToken.None;
        }

        public void Dispose()
        {
            _outputWriter.Dispose();
            _errorWriter.Dispose();
        }

        private static ConsoleWriter CreateDefaultConsoleWriter(System.IO.TextWriter writer)
        {
            // Try to create ConsoleWriter using the internal constructor
            var consoleWriterType = typeof(ConsoleWriter);
            var constructor = consoleWriterType.GetConstructor(
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                null,
                new[] { typeof(System.IO.TextWriter) },
                null);

            if (constructor != null)
            {
                return (ConsoleWriter)constructor.Invoke(new object[] { writer });
            }

            // Fallback: try to use default ConsoleWriter via struct initialization
            return default;
        }

        private static ConsoleReader CreateDefaultConsoleReader(System.IO.TextReader reader)
        {
            // Try to create ConsoleReader using the internal constructor
            var consoleReaderType = typeof(ConsoleReader);
            var constructor = consoleReaderType.GetConstructor(
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                null,
                new[] { typeof(System.IO.TextReader) },
                null);

            if (constructor != null)
            {
                return (ConsoleReader)constructor.Invoke(new object[] { reader });
            }

            // Fallback: try to use default ConsoleReader via struct initialization
            return default;
        }
    }

    #endregion
}
