using System.Text;
using CliFx.Infrastructure;
using FluentAssertions;
using Xunit;

namespace AbpDevTools.Tests;

public class ConsoleSupportTests
{
    [Fact]
    public void ConfirmOrDefault_WithRedirectedConsole_ReturnsDefaultAndWritesFallbackMessage()
    {
        // Arrange
        var console = new TestConsole(isInputRedirected: true, isOutputRedirected: true, isErrorRedirected: true);

        // Act
        var result = ConsoleSupport.ConfirmOrDefault(
            console,
            "Continue?",
            defaultValue: false,
            fallbackMessage: "Using the safe default.",
            confirm: (_, _) => throw new InvalidOperationException("Prompt callback should not run."));

        // Assert
        result.Should().BeFalse();
        console.GetOutput().Should().Contain("Using the safe default.");
    }

    [Fact]
    public void ConfirmOrDefault_WithInteractiveConsole_UsesPromptCallback()
    {
        // Arrange
        var console = new TestConsole(isInputRedirected: false, isOutputRedirected: false, isErrorRedirected: false);
        var callbackInvoked = false;

        // Act
        var result = ConsoleSupport.ConfirmOrDefault(
            console,
            "Continue?",
            defaultValue: false,
            fallbackMessage: "Should not be shown.",
            confirm: (prompt, defaultValue) =>
            {
                callbackInvoked = true;
                prompt.Should().Be("Continue?");
                defaultValue.Should().BeFalse();
                return true;
            });

        // Assert
        result.Should().BeTrue();
        callbackInvoked.Should().BeTrue();
        console.GetOutput().Should().BeEmpty();
    }

    private sealed class TestConsole : IConsole
    {
        private readonly MemoryStream _inputStream = new();
        private readonly MemoryStream _outputStream = new();
        private readonly MemoryStream _errorStream = new();
        private readonly Lazy<ConsoleReader> _input;
        private readonly Lazy<ConsoleWriter> _output;
        private readonly Lazy<ConsoleWriter> _error;
        private int _windowWidth = 120;

        public TestConsole(bool isInputRedirected, bool isOutputRedirected, bool isErrorRedirected)
        {
            IsInputRedirected = isInputRedirected;
            IsOutputRedirected = isOutputRedirected;
            IsErrorRedirected = isErrorRedirected;

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
            get => _windowWidth;
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
