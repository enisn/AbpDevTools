using System.Text;
using AbpDevTools.Commands;
using AbpDevTools.Environments;
using AbpDevTools.Notifications;
using AbpDevTools.Services;
using CliFx.Infrastructure;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AbpDevTools.Tests.Commands;

public class RunCommand_NonInteractiveTests
{
    [Fact]
    public void ClearConsoleIfNeeded_WithRedirectedConsole_DoesNotTouchWindowWidth()
    {
        // Arrange
        var command = new TestRunCommand();
        var console = new TestConsole(isInputRedirected: true, isOutputRedirected: true, isErrorRedirected: true, throwOnWindowWidthAccess: true);
        command.SetConsole(console);

        // Act
        var act = () => command.InvokeClearConsoleIfNeeded();

        // Assert
        act.Should().NotThrow("redirected consoles should skip console width checks entirely");
    }

    [Fact]
    public async Task RenderProcessesWithoutInteractiveConsole_WritesStatusesAndReturns()
    {
        // Arrange
        var command = new TestRunCommand();
        var console = new TestConsole(isInputRedirected: true, isOutputRedirected: true, isErrorRedirected: true);
        command.SetConsole(console);
        command.AddProject(new RunningProjectItem
        {
            Name = "Sample.Web",
            Status = "Building..."
        });

        // Act
        await command.InvokeRenderProcessesWithoutInteractiveConsole(CancellationToken.None);

        // Assert
        var output = console.GetOutput();
        output.Should().Contain("Interactive console features are unavailable");
        output.Should().Contain("Sample.Web");
        output.Should().Contain("Building...");
    }

    [CliFx.Attributes.Command("test-run-command")]
    private sealed class TestRunCommand : RunCommand
    {
        public TestRunCommand()
            : base(
                Substitute.For<INotificationManager>(),
                null!,
                Substitute.For<IProcessEnvironmentManager>(),
                null!,
                null!,
                null!,
                null!,
                null!,
                Substitute.For<IKeyInputManager>())
        {
        }

        public void SetConsole(IConsole console)
        {
            this.console = console;
        }

        public void AddProject(RunningProjectItem runningProject)
        {
            runningProjects.Add(runningProject);
        }

        public void InvokeClearConsoleIfNeeded()
        {
            ClearConsoleIfNeeded();
        }

        public Task InvokeRenderProcessesWithoutInteractiveConsole(CancellationToken cancellationToken)
        {
            return RenderProcessesWithoutInteractiveConsole(cancellationToken);
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
        private readonly bool _throwOnWindowWidthAccess;
        private int _windowWidth = 120;

        public TestConsole(bool isInputRedirected, bool isOutputRedirected, bool isErrorRedirected, bool throwOnWindowWidthAccess = false)
        {
            IsInputRedirected = isInputRedirected;
            IsOutputRedirected = isOutputRedirected;
            IsErrorRedirected = isErrorRedirected;
            _throwOnWindowWidthAccess = throwOnWindowWidthAccess;

            _input = new Lazy<ConsoleReader>(() => new ConsoleReader(this, _inputStream, Encoding.UTF8));
            _output = new Lazy<ConsoleWriter>(() => new ConsoleWriter(this, _outputStream, Encoding.UTF8));
            _error = new Lazy<ConsoleWriter>(() => new ConsoleWriter(this, _errorStream, Encoding.UTF8));
        }

        public ConsoleReader Input => _input.Value;
        public ConsoleWriter Output => _output.Value;
        public ConsoleWriter Error => _error.Value;

        public bool IsOutputRedirected { get; }
        public bool IsErrorRedirected { get; }
        public bool IsInputRedirected { get; }

        public ConsoleColor ForegroundColor { get; set; }
        public ConsoleColor BackgroundColor { get; set; }

        public int WindowWidth
        {
            get
            {
                if (_throwOnWindowWidthAccess)
                {
                    throw new InvalidOperationException("Window width is unavailable.");
                }

                return _windowWidth;
            }
            set => _windowWidth = value;
        }

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
}
