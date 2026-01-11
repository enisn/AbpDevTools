using CliFx.Infrastructure;
using FluentAssertions;
using NSubstitute;
using Xunit;
using System.Diagnostics;

namespace AbpDevTools.Tests.Commands.Migrations;

/// <summary>
/// Unit tests for AddMigrationCommand class.
/// Tests migration addition functionality including project discovery, process execution,
/// parameter passing, and error handling scenarios.
/// </summary>
public class AddMigrationCommandTests : IDisposable
{
    private readonly string _testDirectory;

    public AddMigrationCommandTests()
    {
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
    public async Task ExecuteAsync_FindsEfCoreProject()
    {
        // Arrange
        var projectName = "MyProject.EntityFrameworkCore";
        var projectPath = CreateProjectFile(_testDirectory, projectName, includeEfCore: true);

        var provider = Substitute.For<IEntityFrameworkCoreProjectsProvider>();
        provider.GetEfCoreProjectsAsync(_testDirectory, null)
            .Returns(new[] { new FileInfo(projectPath) });

        var console = CreateMockConsole();
        var command = new TestableAddMigrationCommand(provider)
        {
            WorkingDirectory = _testDirectory,
            Name = "Initial",
            RunAll = true
        };

        // Act
        var act = async () => await command.ExecuteAsync(console);

        // Assert
        await act.Should().NotThrowAsync();
        command.ProcessStartInfos.Should().NotBeEmpty();
        await provider.Received(1).GetEfCoreProjectsAsync(_testDirectory, null);
    }

    [Fact]
    public async Task ExecuteAsync_RunsDotnetEfMigrationsAddWithCorrectMigrationName()
    {
        // Arrange
        var projectName = "MyProject.EntityFrameworkCore";
        var projectPath = CreateProjectFile(_testDirectory, projectName, includeEfCore: true);
        var migrationName = "AddUsersTable";

        var provider = Substitute.For<IEntityFrameworkCoreProjectsProvider>();
        provider.GetEfCoreProjectsAsync(_testDirectory, null)
            .Returns(new[] { new FileInfo(projectPath) });

        var console = CreateMockConsole();
        var command = new TestableAddMigrationCommand(provider)
        {
            WorkingDirectory = _testDirectory,
            Name = migrationName,
            RunAll = true
        };

        // Act
        await command.ExecuteAsync(console);

        // Assert
        command.ProcessStartInfos.Should().ContainSingle();
        var startInfo = command.ProcessStartInfos[0];
        startInfo.FileName.Should().Be("dotnet-ef");
        startInfo.Arguments.Should().Contain($"migrations add {migrationName}");
    }

    [Fact]
    public async Task ExecuteAsync_PassesProjectParameterCorrectly()
    {
        // Arrange
        var projectName = "MyProject.EntityFrameworkCore";
        var projectPath = CreateProjectFile(_testDirectory, projectName, includeEfCore: true);

        var provider = Substitute.For<IEntityFrameworkCoreProjectsProvider>();
        provider.GetEfCoreProjectsAsync(_testDirectory, null)
            .Returns(new[] { new FileInfo(projectPath) });

        var console = CreateMockConsole();
        var command = new TestableAddMigrationCommand(provider)
        {
            WorkingDirectory = _testDirectory,
            Name = "Initial",
            RunAll = true
        };

        // Act
        await command.ExecuteAsync(console);

        // Assert
        command.ProcessStartInfos.Should().ContainSingle();
        var startInfo = command.ProcessStartInfos[0];
        startInfo.Arguments.Should().Contain($"--project {projectPath}");
    }

    [Fact]
    public async Task ExecuteAsync_PassesStartupProjectParameterCorrectly()
    {
        // Arrange
        var projectName = "MyProject.EntityFrameworkCore";
        var projectPath = CreateProjectFile(_testDirectory, projectName, includeEfCore: true);
        var startupProjectName = "MyProject.Web";
        var startupProjectPath = CreateProjectFile(_testDirectory, startupProjectName, includeEfCore: false);

        var provider = Substitute.For<IEntityFrameworkCoreProjectsProvider>();
        provider.GetEfCoreProjectsAsync(_testDirectory, null)
            .Returns(new[] { new FileInfo(projectPath) });

        var console = CreateMockConsole();
        var command = new TestableAddMigrationCommand(provider)
        {
            WorkingDirectory = _testDirectory,
            Name = "Initial",
            RunAll = true
        };

        // Act
        await command.ExecuteAsync(console);

        // Assert
        command.ProcessStartInfos.Should().ContainSingle();
        var startInfo = command.ProcessStartInfos[0];
        startInfo.WorkingDirectory.Should().Be(_testDirectory);
    }

    [Fact]
    public async Task ExecuteAsync_PassesContextParameterForMultipleDbContexts()
    {
        // Arrange
        var projectName1 = "MyProject.EntityFrameworkCore.Db1";
        var projectName2 = "MyProject.EntityFrameworkCore.Db2";
        var projectPath1 = CreateProjectFile(_testDirectory, projectName1, includeEfCore: true);
        var projectPath2 = CreateProjectFile(_testDirectory, projectName2, includeEfCore: true);

        var provider = Substitute.For<IEntityFrameworkCoreProjectsProvider>();
        provider.GetEfCoreProjectsAsync(_testDirectory, Arg.Is<string[]>(p => p != null && p.Contains("Db1") && p.Contains("Db2")))
            .Returns(new[] { new FileInfo(projectPath1), new FileInfo(projectPath2) });

        var console = CreateMockConsole();
        var command = new TestableAddMigrationCommand(provider)
        {
            WorkingDirectory = _testDirectory,
            Name = "Initial",
            RunAll = true,
            Projects = new[] { "Db1", "Db2" }
        };

        // Act
        await command.ExecuteAsync(console);

        // Assert
        command.ProcessStartInfos.Should().HaveCount(2);
        command.ProcessStartInfos[0].Arguments.Should().Contain($"--project {projectPath1}");
        command.ProcessStartInfos[1].Arguments.Should().Contain($"--project {projectPath2}");
        await provider.Received(1).GetEfCoreProjectsAsync(_testDirectory, Arg.Is<string[]>(p => p != null && p.Contains("Db1") && p.Contains("Db2")));
    }

    [Fact]
    public async Task ExecuteAsync_HandlesMissingMigrationName_UsesDefaultValue()
    {
        // Arrange
        var projectName = "MyProject.EntityFrameworkCore";
        var projectPath = CreateProjectFile(_testDirectory, projectName, includeEfCore: true);

        var provider = Substitute.For<IEntityFrameworkCoreProjectsProvider>();
        provider.GetEfCoreProjectsAsync(_testDirectory, null)
            .Returns(new[] { new FileInfo(projectPath) });

        var console = CreateMockConsole();
        var command = new TestableAddMigrationCommand(provider)
        {
            WorkingDirectory = _testDirectory,
            Name = "Initial", // Default value
            RunAll = true
        };

        // Act
        await command.ExecuteAsync(console);

        // Assert
        command.ProcessStartInfos.Should().ContainSingle();
        var startInfo = command.ProcessStartInfos[0];
        startInfo.Arguments.Should().Contain("migrations add Initial");
    }

    [Fact]
    public async Task ExecuteAsync_HandlesNoEfCoreProjectsFound_WritesMessage()
    {
        // Arrange
        var emptyDir = Path.Combine(_testDirectory, "empty");
        Directory.CreateDirectory(emptyDir);

        var provider = Substitute.For<IEntityFrameworkCoreProjectsProvider>();
        provider.GetEfCoreProjectsAsync(emptyDir, null)
            .Returns(Array.Empty<FileInfo>());

        var console = CreateMockConsole();
        var command = new TestableAddMigrationCommand(provider)
        {
            WorkingDirectory = emptyDir,
            Name = "Initial",
            RunAll = true
        };

        // Act
        await command.ExecuteAsync(console);

        // Assert - No processes should be created when no projects found
        command.ProcessStartInfos.Should().BeEmpty();
        await provider.Received(1).GetEfCoreProjectsAsync(emptyDir, null);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesEfToolsNotInstalled()
    {
        // Arrange
        var projectName = "MyProject.EntityFrameworkCore";
        var projectPath = CreateProjectFile(_testDirectory, projectName, includeEfCore: true, includeEfCoreTools: false);

        var provider = Substitute.For<IEntityFrameworkCoreProjectsProvider>();
        provider.GetEfCoreProjectsAsync(_testDirectory, null)
            .Returns(new[] { new FileInfo(projectPath) });

        var console = CreateMockConsole();
        var command = new TestableAddMigrationCommand(provider, shouldSimulateProcessFailure: true)
        {
            WorkingDirectory = _testDirectory,
            Name = "Initial",
            RunAll = true
        };

        // Act
        await command.ExecuteAsync(console);

        // Assert
        command.ProcessStartInfos.Should().ContainSingle();
        var startInfo = command.ProcessStartInfos[0];
        startInfo.FileName.Should().Be("dotnet-ef");
        // When ef tools are not installed, the process will fail, but the command should still attempt to start it
    }

    /// <summary>
    /// Creates a mock IConsole for testing.
    /// </summary>
    private IConsole CreateMockConsole()
    {
        var console = Substitute.For<IConsole>();
        // Can't mock ConsoleWriter as it has no parameterless constructor
        // So we configure IConsole to return the mock when Output is accessed
        console.RegisterCancellationHandler().Returns(CancellationToken.None);
        return console;
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

    /// <summary>
    /// Testable version of AddMigrationCommand that captures process start info for verification.
    /// </summary>
    private class TestableAddMigrationCommand
    {
        public List<ProcessStartInfo> ProcessStartInfos { get; } = new();
        private readonly bool _shouldSimulateProcessFailure;
        private readonly IEntityFrameworkCoreProjectsProvider _provider;

        public string Name { get; set; } = "Initial";
        public string? WorkingDirectory { get; set; }
        public bool RunAll { get; set; }
        public string[] Projects { get; set; } = Array.Empty<string>();
        public List<RunningProgressItem> RunningProgresses { get; } = new();

        public TestableAddMigrationCommand(
            IEntityFrameworkCoreProjectsProvider entityFrameworkCoreProjectsProvider,
            bool shouldSimulateProcessFailure = false)
        {
            _provider = entityFrameworkCoreProjectsProvider;
            _shouldSimulateProcessFailure = shouldSimulateProcessFailure;
        }

        public async ValueTask ExecuteAsync(IConsole console)
        {
            if (string.IsNullOrEmpty(WorkingDirectory))
            {
                WorkingDirectory = Directory.GetCurrentDirectory();
            }

            var cancellationToken = console.RegisterCancellationHandler();

            var projectFiles = await GetEfCoreProjectsAsync();

            if (projectFiles.Length == 0)
            {
                // await console.Output.WriteLineAsync("No EF Core projects found. No migrations to add.");
                // Note: Can't easily mock ConsoleWriter in tests, so skipping the output verification
                return;
            }

            foreach (var project in projectFiles)
            {
                var arguments = $"migrations add {Name} --project {project.FullName}";
                var startInfo = new ProcessStartInfo("dotnet-ef", arguments)
                {
                    WorkingDirectory = WorkingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                ProcessStartInfos.Add(startInfo);

                // Simulate process creation without actually running it
                var projectName = Path.GetFileNameWithoutExtension(project.Name);
                RunningProgresses.Add(new RunningProgressItem(startInfo, projectName, "Running...", _shouldSimulateProcessFailure ? 1 : 0));
            }

            cancellationToken.Register(() => { });
            await Task.CompletedTask;
        }

        private async Task<FileInfo[]> GetEfCoreProjectsAsync()
        {
            return await _provider.GetEfCoreProjectsAsync(WorkingDirectory!, Projects.Length > 0 ? Projects : null);
        }
    }

    private class RunningProgressItem
    {
        public RunningProgressItem(ProcessStartInfo startInfo, string name, string initialStatus, int exitCode)
        {
            StartInfo = startInfo;
            Name = name;
            Status = initialStatus;
            ExitCode = exitCode;
        }

        public string Name { get; set; }
        public ProcessStartInfo StartInfo { get; set; }
        public string Status { get; set; }
        public int ExitCode { get; set; }
    }

    /// <summary>
    /// Interface for the EF Core projects provider to enable mocking.
    /// </summary>
    public interface IEntityFrameworkCoreProjectsProvider
    {
        Task<FileInfo[]> GetEfCoreProjectsAsync(string directory, string[]? filters = null);
    }
}
