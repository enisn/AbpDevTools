using AbpDevTools.Commands;
using AbpDevTools.Configuration;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using FluentAssertions;
using NSubstitute;
using Xunit;
using YamlDotNet.Serialization;

namespace AbpDevTools.Tests.Commands;

public class TemplateCreateCommandTests : IDisposable
{
    private readonly string _testRootPath;

    public TemplateCreateCommandTests()
    {
        _testRootPath = Path.Combine(Path.GetTempPath(), $"AbpDevTools_TemplateCommand_Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testRootPath);
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
        }
    }

    [Fact]
    public async Task TemplateList_OutputsExpectedTemplateNames()
    {
        // Arrange
        var command = new TemplateListCommand();
        using var console = new TestConsole();

        // Act
        await command.ExecuteAsync(console);
        var output = console.GetOutput();

        // Assert
        output.Should().Contain("dotnet");
        output.Should().Contain("npm");
    }

    [Fact]
    public async Task Create_WithoutForce_RefusesNonEmptyOutputDirectory()
    {
        // Arrange
        var outputPath = Path.Combine(_testRootPath, "existing-output");
        Directory.CreateDirectory(outputPath);
        await File.WriteAllTextAsync(Path.Combine(outputPath, "existing.txt"), "stale");

        var command = new TemplateCreateCommand(new MockToolsConfiguration())
        {
            TemplateType = "npm",
            Name = "acme.package",
            Output = outputPath,
            Force = false
        };

        using var console = new TestConsole();

        // Act
        var action = async () => await command.ExecuteAsync(console);

        // Assert
        var exception = await Assert.ThrowsAsync<CommandException>(action);
        exception.Message.Should().Contain("already exists and is not empty");
    }

    [Fact]
    public async Task Create_WithForce_OverwritesExistingNonEmptyDirectory_ForNpmTemplate()
    {
        // Arrange
        var outputPath = Path.Combine(_testRootPath, "npm-template-output");
        Directory.CreateDirectory(outputPath);
        await File.WriteAllTextAsync(Path.Combine(outputPath, "stale.txt"), "old content");
        Directory.CreateDirectory(Path.Combine(outputPath, "old-folder"));

        var command = new TemplateCreateCommand(new MockToolsConfiguration())
        {
            TemplateType = "npm",
            Name = "acme.package",
            Description = "Template test package",
            Output = outputPath,
            Force = true
        };

        using var console = new TestConsole();
        var originalCurrentDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(FindRepositoryRoot());

            // Act
            await command.ExecuteAsync(console);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCurrentDirectory);
        }

        // Assert
        File.Exists(Path.Combine(outputPath, "stale.txt")).Should().BeFalse();
        Directory.Exists(Path.Combine(outputPath, "old-folder")).Should().BeFalse();

        var packageJsonPath = Path.Combine(outputPath, "package.json");
        File.Exists(packageJsonPath).Should().BeTrue();

        var packageJson = await File.ReadAllTextAsync(packageJsonPath);
        packageJson.Should().Contain("acme.package");
        packageJson.Should().NotContain("__PACKAGE_NAME__");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var templatesPath = Path.Combine(directory.FullName, "templates", "npm", "abp-package-simple");
            if (Directory.Exists(templatesPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root with templates folder.");
    }

    private sealed class MockToolsConfiguration : ToolsConfiguration
    {
        public MockToolsConfiguration()
            : base(Substitute.For<IDeserializer>(), Substitute.For<ISerializer>())
        {
        }

        public override ToolOption GetOptions()
        {
            return new ToolOption
            {
                ["dotnet"] = "dotnet",
                ["powershell"] = "pwsh",
                ["abp"] = "abp"
            };
        }
    }

    private sealed class TestConsole : IConsole, IDisposable
    {
        private readonly MemoryStream _inputStream = new();
        private readonly MemoryStream _outputStream = new();
        private readonly MemoryStream _errorStream = new();

        public ConsoleReader Input { get; }
        public ConsoleWriter Output { get; }
        public ConsoleWriter Error { get; }

        public bool IsInputRedirected => false;
        public bool IsOutputRedirected => false;
        public bool IsErrorRedirected => false;

        public ConsoleColor ForegroundColor { get; set; }
        public ConsoleColor BackgroundColor { get; set; }
        public int WindowWidth { get; set; } = 120;
        public int WindowHeight { get; set; } = 30;
        public int CursorLeft { get; set; }
        public int CursorTop { get; set; }

        public TestConsole()
        {
            Input = new ConsoleReader(this, _inputStream);
            Output = new ConsoleWriter(this, _outputStream);
            Error = new ConsoleWriter(this, _errorStream);
        }

        public string GetOutput()
        {
            Output.Flush();
            _outputStream.Position = 0;
            using var reader = new StreamReader(_outputStream, leaveOpen: true);
            return reader.ReadToEnd();
        }

        public CancellationToken RegisterCancellationHandler() => CancellationToken.None;
        public ConsoleKeyInfo ReadKey(bool intercept = false) => default;
        public void ResetColor() { }
        public void Clear() { }

        public void Dispose()
        {
            Input.Dispose();
            Output.Dispose();
            Error.Dispose();
            _inputStream.Dispose();
            _outputStream.Dispose();
            _errorStream.Dispose();
        }
    }
}
