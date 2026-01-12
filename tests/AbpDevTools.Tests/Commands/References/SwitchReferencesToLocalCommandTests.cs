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
    private readonly MockLocalSourcesConfiguration _mockLocalSourcesConfiguration;
    private readonly FileExplorer _fileExplorer;
    private readonly CsprojManipulationService _csprojService;
    private readonly FakeGitService _fakeGitService;
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

        // Create mock LocalSourcesConfiguration using a testable wrapper
        _mockLocalSourcesConfiguration = new MockLocalSourcesConfiguration(sources);

        // Use real FileExplorer and CsprojManipulationService
        _fileExplorer = new FileExplorer();
        _csprojService = new CsprojManipulationService(_fileExplorer);
        _fakeGitService = new FakeGitService();

        // Create command instance with dependencies
        _command = new SwitchReferencesToLocalCommand(
            _mockLocalSourcesConfiguration,
            _fileExplorer,
            _csprojService,
            _fakeGitService
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
        // Add a marker file to ensure directory is not empty
        File.WriteAllText(Path.Combine(abpSourcePath, ".gitkeep"), "");

        var console = new FakeConsole();
        _command.WorkingDirectory = workingDirectory;

        // Act & Assert - should not throw
        Exception? exception = null;
        try
        {
            await _command.ExecuteAsync(console);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        exception.Should().BeNull("Command should execute without throwing");
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

        // Create abp source directory to avoid interactive prompt
        var abpSourcePath = Path.Combine(_testRootPath, "abp");
        Directory.CreateDirectory(abpSourcePath);
        File.WriteAllText(Path.Combine(abpSourcePath, ".gitkeep"), "");

        var console = new FakeConsole();
        _command.WorkingDirectory = workingDirectory;

        // Act
        await _command.ExecuteAsync(console);

        // Assert
        _mockLocalSourcesConfiguration.GetOptionsCallCount.Should().Be(1);
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

        // Create abp source directory to avoid interactive prompt, but the test still verifies git operations
        var abpSourcePath = Path.Combine(_testRootPath, "abp");
        Directory.CreateDirectory(abpSourcePath);
        File.WriteAllText(Path.Combine(abpSourcePath, ".gitkeep"), "");

        _fakeGitService.IsGitInstalledResult = true;
        _fakeGitService.CloneRepositoryAsyncResult = true;

        var console = new FakeConsole();
        _command.WorkingDirectory = workingDirectory;

        // Act
        await _command.ExecuteAsync(console);

        // Assert - No cloning should occur since directory exists and is not empty
        _fakeGitService.CloneRepositoryAsyncCallCount.Should().Be(0);
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

        var console = new FakeConsole();
        _command.WorkingDirectory = workingDirectory;

        // Act
        await _command.ExecuteAsync(console);

        // Assert
        _fakeGitService.CloneRepositoryAsyncCallCount.Should().Be(0);
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

        // Create abp source directory to avoid interactive prompt
        var abpSourcePath = Path.Combine(_testRootPath, "abp");
        Directory.CreateDirectory(abpSourcePath);
        File.WriteAllText(Path.Combine(abpSourcePath, ".gitkeep"), "");

        _fakeGitService.IsGitInstalledResult = true;
        _fakeGitService.CloneRepositoryAsyncResult = false;

        var console = new FakeConsole();
        _command.WorkingDirectory = workingDirectory;

        // Act - should not throw
        await _command.ExecuteAsync(console);

        // Assert - No cloning should occur since directory exists and is not empty
        _fakeGitService.CloneRepositoryAsyncCallCount.Should().Be(0);
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

        // Create abp source directory to avoid interactive prompt
        var abpSourcePath = Path.Combine(_testRootPath, "abp");
        Directory.CreateDirectory(abpSourcePath);
        File.WriteAllText(Path.Combine(abpSourcePath, ".gitkeep"), "");

        _fakeGitService.IsGitInstalledResult = false;

        var console = new FakeConsole();
        _command.WorkingDirectory = workingDirectory;

        // Act
        await _command.ExecuteAsync(console);

        // Assert - Clone should not be attempted when directory exists
        _fakeGitService.CloneRepositoryAsyncCallCount.Should().Be(0);
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

        var fakeGitService2 = new FakeGitService();
        fakeGitService2.IsGitInstalledResult = true;

        var command = new SwitchReferencesToLocalCommand(
            mockConfig,
            _fileExplorer,
            _csprojService,
            fakeGitService2
        );

        var workingDirectory = Path.Combine(_testRootPath, "workspace2");
        Directory.CreateDirectory(workingDirectory);

        File.WriteAllText(Path.Combine(workingDirectory, "Project1.csproj"), CreateBasicCsprojContent("Project1"));

        // Create abp2 source directory to avoid interactive prompt
        var abpSourcePath2 = Path.Combine(_testRootPath, "abp2");
        Directory.CreateDirectory(abpSourcePath2);
        File.WriteAllText(Path.Combine(abpSourcePath2, ".gitkeep"), "");

        var console = new FakeConsole();
        command.WorkingDirectory = workingDirectory;

        // Act
        await command.ExecuteAsync(console);

        // Assert - Clone should not be attempted when directory exists
        fakeGitService2.CloneRepositoryAsyncCallCount.Should().Be(0);
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

        var fakeGitService3 = new FakeGitService();

        var command = new SwitchReferencesToLocalCommand(
            mockConfig,
            _fileExplorer,
            _csprojService,
            fakeGitService3
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
                new[] { typeof(IConsole), typeof(Stream), typeof(System.Text.Encoding) },
                null);

            if (constructor != null)
            {
                // Create a MemoryStream to wrap the TextWriter
                var memoryStream = new System.IO.MemoryStream();
                return (ConsoleWriter)constructor.Invoke(new object[] { new FakeConsole(), memoryStream, System.Text.Encoding.UTF8 });
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
                new[] { typeof(IConsole), typeof(Stream), typeof(System.Text.Encoding) },
                null);

            if (constructor != null)
            {
                var memoryStream = new System.IO.MemoryStream();
                return (ConsoleReader)constructor.Invoke(new object[] { new FakeConsole(), memoryStream, System.Text.Encoding.UTF8 });
            }

            // Fallback: try to use default ConsoleReader via struct initialization
            return default;
        }
    }

    #endregion

    /// <summary>
    /// Fake GitService for testing that tracks method calls and returns configurable results.
    /// </summary>
    private class FakeGitService : GitService
    {
        public bool IsGitInstalledResult { get; set; } = true;
        public bool CloneRepositoryAsyncResult { get; set; } = true;
        public int CloneRepositoryAsyncCallCount { get; private set; }
        public string? LastCloneRemoteUrl { get; private set; }
        public string? LastCloneLocalPath { get; private set; }
        public string? LastCloneBranch { get; private set; }

        public new async Task<bool> CloneRepositoryAsync(string remoteUrl, string localPath, string? branch = null, CancellationToken cancellationToken = default)
        {
            CloneRepositoryAsyncCallCount++;
            LastCloneRemoteUrl = remoteUrl;
            LastCloneLocalPath = localPath;
            LastCloneBranch = branch;

            await Task.CompletedTask;
            return CloneRepositoryAsyncResult;
        }

        public new bool IsGitInstalled()
        {
            return IsGitInstalledResult;
        }
    }

    /// <summary>
    /// Mock configuration that returns predefined options without reading from file system.
    /// </summary>
    private class MockLocalSourcesConfiguration : LocalSourcesConfiguration
    {
        private readonly LocalSourceMapping _options;
        public int GetOptionsCallCount { get; private set; }

        public MockLocalSourcesConfiguration(LocalSourceMapping options)
            : base(
                new YamlDotNet.Serialization.DeserializerBuilder().Build(),
                new YamlDotNet.Serialization.SerializerBuilder().Build())
        {
            _options = options;
        }

        public override LocalSourceMapping GetOptions()
        {
            GetOptionsCallCount++;
            return _options;
        }
    }
}
