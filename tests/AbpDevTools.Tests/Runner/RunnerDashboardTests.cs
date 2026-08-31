using System.Text;
using AbpDevTools.Runner;
using CliFx.Infrastructure;
using Shouldly;
using Spectre.Console;
using Spectre.Console.Rendering;
using Xunit;

namespace AbpDevTools.Tests.Runner;

public sealed class RunnerDashboardTests
{
    [Fact]
    public void BuildView_UsesTheRemainingViewportForLogs()
    {
        var console = new TestConsole { WindowWidth = 80, WindowHeight = 24 };
        var logs = Enumerable.Range(0, 100)
            .Select(index => new RunnerLogEntry
            {
                Sequence = index + 1,
                Timestamp = DateTimeOffset.UtcNow,
                ApplicationId = "app-0",
                ApplicationName = "App 0",
                Message = $"log-{index} {new string('x', 120)}"
            })
            .ToArray();

        var oneApplication = Render(
            RunnerDashboard.BuildView(CreateContext(1), 0, false, logs, console),
            console);
        var fourApplications = Render(
            RunnerDashboard.BuildView(CreateContext(4), 0, false, logs, console),
            console);
        console.WindowHeight = 32;
        var resized = Render(
            RunnerDashboard.BuildView(CreateContext(4), 0, false, logs, console),
            console);

        oneApplication.Count.ShouldBe(24);
        fourApplications.Count.ShouldBe(24);
        resized.Count.ShouldBe(32);
        FindLine(fourApplications, "Logs: App 0").ShouldBeGreaterThan(FindLine(oneApplication, "Logs: App 0"));
        FindLine(fourApplications, "↑/↓ or J/K").ShouldBeGreaterThan(FindLine(fourApplications, "Logs: App 0"));
        FindLine(resized, "↑/↓ or J/K").ShouldBeGreaterThan(FindLine(fourApplications, "↑/↓ or J/K"));
        string.Join(Environment.NewLine, fourApplications).ShouldContain("log-99");
    }

    [Fact]
    public void ClearInteractiveSurface_ClearsTheConsoleOnce()
    {
        var console = new TestConsole();

        RunnerDashboard.ClearInteractiveSurface(console);

        console.ClearCount.ShouldBe(1);
    }

    [Fact]
    public void BuildLogView_FillsTheViewportAndShowsLiveOrPausedPosition()
    {
        var console = new TestConsole { WindowWidth = 80, WindowHeight = 24 };
        var context = CreateContext(2);
        var logs = CreateLogs(100);

        var live = Render(
            RunnerDashboard.BuildLogView(context, 0, false, logs, 0, console),
            console);
        var paused = Render(
            RunnerDashboard.BuildLogView(context, 0, false, logs, 10, console),
            console);
        console.WindowHeight = 32;
        var resized = Render(
            RunnerDashboard.BuildLogView(context, 0, false, logs, 10, console),
            console);

        live.Count.ShouldBe(24);
        paused.Count.ShouldBe(24);
        resized.Count.ShouldBe(32);
        string.Join(Environment.NewLine, live).ShouldContain("Logs: App 0 • LIVE");
        string.Join(Environment.NewLine, live).ShouldContain("log-99");
        string.Join(Environment.NewLine, paused).ShouldContain("PAUSED • 10 newer entries");
        string.Join(Environment.NewLine, paused).ShouldContain("log-89");
        string.Join(Environment.NewLine, paused).ShouldNotContain("log-99");
    }

    [Fact]
    public void AdjustLogScrollOffset_ScrollsPagesAndResumesFollowing()
    {
        var console = new TestConsole { WindowWidth = 100, WindowHeight = 16 };
        var logs = CreateLogs(100);

        var oneLineUp = RunnerDashboard.AdjustLogScrollOffset(
            ConsoleKey.UpArrow,
            0,
            logs,
            console);
        var onePageUp = RunnerDashboard.AdjustLogScrollOffset(
            ConsoleKey.PageUp,
            0,
            logs,
            console);
        var oldest = RunnerDashboard.AdjustLogScrollOffset(
            ConsoleKey.Home,
            0,
            logs,
            console);

        oneLineUp.ShouldBe(1);
        onePageUp.ShouldBeGreaterThan(oneLineUp);
        oldest.ShouldBeGreaterThan(onePageUp);
        var oldestPage = Render(
            RunnerDashboard.BuildLogView(CreateContext(1), 0, false, logs, oldest, console),
            console);
        string.Join(Environment.NewLine, oldestPage).ShouldContain("] log-0");
        string.Join(Environment.NewLine, oldestPage).ShouldNotContain("] log-99");
        RunnerDashboard.AdjustLogScrollOffset(ConsoleKey.End, oldest, logs, console).ShouldBe(0);
        RunnerDashboard.AdjustLogScrollOffset(ConsoleKey.F, oldest, logs, console).ShouldBe(0);
    }

    private static RunnerContextSnapshot CreateContext(int applicationCount)
    {
        return new RunnerContextSnapshot
        {
            ContextKey = "context",
            DisplayName = "Sample",
            WorkingDirectory = Path.GetTempPath(),
            Applications = Enumerable.Range(0, applicationCount)
                .Select(index => new RunnerApplicationSnapshot
                {
                    Id = $"app-{index}",
                    Name = $"App {index}",
                    DisplayName = $"App {index}",
                    State = RunnerApplicationState.Running,
                    Readiness = RunnerReadinessState.Ready,
                    ProcessId = 1000 + index,
                    Status = "Running"
                })
                .ToArray()
        };
    }

    private static RunnerLogEntry[] CreateLogs(int count)
    {
        return Enumerable.Range(0, count)
            .Select(index => new RunnerLogEntry
            {
                Sequence = index + 1,
                Timestamp = DateTimeOffset.UtcNow,
                ApplicationId = "app-0",
                ApplicationName = "App 0",
                Message = $"log-{index}"
            })
            .ToArray();
    }

    private static IReadOnlyList<string> Render(IRenderable renderable, TestConsole console)
    {
        var options = new RenderOptions(
            AnsiConsole.Console.Profile.Capabilities,
            new Size(console.WindowWidth, console.WindowHeight));
        return Segment.SplitLines(
                renderable.Render(options, console.WindowWidth),
                console.WindowWidth)
            .Select(line => string.Concat(line.Select(segment => segment.Text)))
            .ToArray();
    }

    private static int FindLine(IReadOnlyList<string> lines, string value)
    {
        var matches = lines
            .Select((line, index) => (line, index))
            .Where(item => item.line.Contains(value, StringComparison.Ordinal))
            .Select(item => item.index)
            .ToArray();
        matches.ShouldNotBeEmpty();
        return matches[0];
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
        public bool IsOutputRedirected => false;
        public bool IsErrorRedirected => false;
        public bool IsInputRedirected => false;
        public ConsoleColor ForegroundColor { get; set; }
        public ConsoleColor BackgroundColor { get; set; }
        public int WindowWidth { get; set; } = 120;
        public int WindowHeight { get; set; } = 30;
        public int CursorLeft { get; set; }
        public int CursorTop { get; set; }
        public int ClearCount { get; private set; }

        public CancellationToken RegisterCancellationHandler() => CancellationToken.None;
        public ConsoleKeyInfo ReadKey(bool intercept = false) => default;
        public void Clear() => ClearCount++;
        public void ResetColor() { }
    }
}
