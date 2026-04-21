using System.Text;
using AbpDevTools.Commands;
using AbpDevTools.Configuration;
using AbpDevTools.Services;
using CliFx.Infrastructure;
using FluentAssertions;
using NSubstitute;
using Shouldly;
using Xunit;
using YamlDotNet.Serialization;

namespace AbpDevTools.Tests.Commands;

public class LogsCommandTests : IDisposable
{
    private readonly string _testRootPath;
    private readonly RunnableProjectsProvider _runnableProjectsProvider;

    public LogsCommandTests()
    {
        _testRootPath = Path.Combine(Path.GetTempPath(), $"AbpDevTools_LogsCommand_Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testRootPath);

        _runnableProjectsProvider = new RunnableProjectsProvider(
            new RunConfiguration(Substitute.For<IDeserializer>(), Substitute.For<ISerializer>()));
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
    public void LogsCommand_Defaults_ToTailing100Lines()
    {
        var command = CreateCommand();

        command.Lines.ShouldBe(100);
        command.OpenWithDefaultApp.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_PrintsLastRequestedLines_ByDefault()
    {
        CreateRunnableProjectWithLogs("MyApp.Web", Enumerable.Range(1, 5).Select(i => $"line {i}"));

        var command = CreateCommand();
        command.WorkingDirectory = _testRootPath;
        command.ProjectName = "MyApp.Web";
        command.Lines = 3;
        var console = new TestConsole();

        await command.ExecuteAsync(console);

        var output = console.GetOutput();
        output.ShouldContain("Showing last 3 line(s)");
        output.ShouldContain("line 3");
        output.ShouldContain("line 4");
        output.ShouldContain("line 5");
        output.ShouldNotContain("line 2");
    }

    [Fact]
    public async Task ExecuteAsync_WithOpenOption_UsesPlatformOpen()
    {
        var logFilePath = CreateRunnableProjectWithLogs("MyApp.Web", new[] { "line 1" });
        var platform = new TestPlatform();
        var command = CreateCommand(platform);
        command.WorkingDirectory = _testRootPath;
        command.ProjectName = "MyApp.Web";
        command.OpenWithDefaultApp = true;
        var console = new TestConsole();

        await command.ExecuteAsync(console);

        platform.OpenedPaths.Should().ContainSingle();
        platform.OpenedPaths[0].ShouldBe(logFilePath);
    }

    [Fact]
    public async Task ExecuteAsync_WhenLogsFileDoesNotExist_WritesHelpfulMessage()
    {
        CreateRunnableProject("MyApp.Web");

        var command = CreateCommand();
        command.WorkingDirectory = _testRootPath;
        command.ProjectName = "MyApp.Web";
        var console = new TestConsole();

        await command.ExecuteAsync(console);

        var output = console.GetOutput();
        output.ShouldContain("No logs folder found for project 'MyApp.Web.csproj'");
        output.ShouldContain("--open");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidLines_WritesError()
    {
        CreateRunnableProjectWithLogs("MyApp.Web", new[] { "line 1" });

        var command = CreateCommand();
        command.WorkingDirectory = _testRootPath;
        command.ProjectName = "MyApp.Web";
        command.Lines = 0;
        var console = new TestConsole();

        await command.ExecuteAsync(console);

        console.GetError().ShouldContain("'--lines' option must be greater than 0");
    }

    private LogsCommand CreateCommand(Platform? platform = null)
    {
        return new LogsCommand(_runnableProjectsProvider, platform ?? new TestPlatform());
    }

    private string CreateRunnableProject(string projectName)
    {
        var projectDir = Path.Combine(_testRootPath, projectName);
        Directory.CreateDirectory(projectDir);

        File.WriteAllText(Path.Combine(projectDir, $"{projectName}.csproj"), @"<Project Sdk=""Microsoft.NET.Sdk.Web""><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(projectDir, "Program.cs"), "Console.WriteLine(\"Hello\");");

        return projectDir;
    }

    private string CreateRunnableProjectWithLogs(string projectName, IEnumerable<string> lines)
    {
        var projectDir = CreateRunnableProject(projectName);
        var logsDir = Path.Combine(projectDir, "Logs");
        Directory.CreateDirectory(logsDir);

        var logFilePath = Path.Combine(logsDir, "logs.txt");
        File.WriteAllLines(logFilePath, lines, Encoding.UTF8);
        return logFilePath;
    }

    private sealed class TestPlatform : Platform
    {
        public List<string> OpenedPaths { get; } = new();

        public override void Open(string filePath)
        {
            OpenedPaths.Add(filePath);
        }

        public override Task OpenAsync(string filePath)
        {
            OpenedPaths.Add(filePath);
            return Task.CompletedTask;
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

        public string GetError()
        {
            if (_error.IsValueCreated)
            {
                _error.Value.Flush();
            }

            return Encoding.UTF8.GetString(_errorStream.ToArray());
        }
    }
}
