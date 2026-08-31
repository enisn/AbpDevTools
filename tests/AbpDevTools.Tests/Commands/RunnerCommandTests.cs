using System.Text;
using AbpDevTools.Commands;
using AbpDevTools.Environments;
using AbpDevTools.LocalConfigurations;
using AbpDevTools.Runner;
using AbpDevTools.Services;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using NSubstitute;
using Shouldly;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AbpDevTools.Tests.Commands;

public sealed class RunnerCommandTests : IDisposable
{
    private readonly string _root;
    private readonly IRunnerClient _runnerClient = Substitute.For<IRunnerClient>();
    private readonly IRunnerDashboard _dashboard = Substitute.For<IRunnerDashboard>();
    private readonly RunnerContextResolver _contextResolver;

    public RunnerCommandTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "AbpDevTools_RunnerCommand_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        var localConfigurationManager = new LocalConfigurationManager(
            new DeserializerBuilder().WithNamingConvention(HyphenatedNamingConvention.Instance).Build(),
            new SerializerBuilder().WithNamingConvention(HyphenatedNamingConvention.Instance).Build(),
            new FileExplorer(),
            Substitute.For<IProcessEnvironmentManager>());
        _contextResolver = new RunnerContextResolver(localConfigurationManager);
    }

    [Fact]
    public async Task Stop_UsesOnlyTheResolvedDirectoryContext_AndForwardsProjectSelectors()
    {
        var descriptor = RunnerContextIdentity.Create(_root, null);
        var selectors = new[] { "api", "web" };
        _runnerClient.StopAsync(
                descriptor.ContextKey,
                Arg.Is<string[]>(x => x.SequenceEqual(selectors)),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RunnerResponse?>(new RunnerResponse
            {
                AffectedCount = 2,
                Messages = new[] { "Stopped 2 application(s)." }
            }));
        var command = new StopCommand(_runnerClient, _contextResolver)
        {
            WorkingDirectory = _root,
            Projects = selectors
        };
        var console = new TestConsole();

        await command.ExecuteAsync(console);

        await _runnerClient.Received(1).StopAsync(
            descriptor.ContextKey,
            Arg.Is<string[]>(x => x.SequenceEqual(selectors)),
            Arg.Any<CancellationToken>());
        console.GetOutput().ShouldContain("Stopped 2 application(s).");
    }

    [Fact]
    public async Task Stop_WithoutArgument_UsesTheCurrentActiveContext()
    {
        var requestedContext = _contextResolver.Resolve(null);
        var currentContext = CreateContext(
            requestedContext.WorkingDirectory,
            "Current.Web",
            requestedContext.ConfigurationPath);
        var unrelatedContext = CreateContext(Path.Combine(_root, "unrelated"), "Other.Web");
        _runnerClient.ListAsync(null, false, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RunnerResponse?>(new RunnerResponse
            {
                Contexts = new[] { unrelatedContext, currentContext }
            }));
        _runnerClient.StopAsync(
                currentContext.ContextKey,
                Arg.Is<string[]>(x => x.Length == 0),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RunnerResponse?>(new RunnerResponse
            {
                AffectedCount = 1,
                Messages = new[] { "Stopped 1 application(s)." }
            }));
        var command = new StopCommand(_runnerClient, _contextResolver);
        var console = new TestConsole();

        await command.ExecuteAsync(console);

        await _runnerClient.Received(1).StopAsync(
            currentContext.ContextKey,
            Arg.Is<string[]>(x => x.Length == 0),
            Arg.Any<CancellationToken>());
        await _runnerClient.DidNotReceive().StopAsync(
            unrelatedContext.ContextKey,
            Arg.Any<string[]>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stop_WithoutLocalContext_NonInteractiveListsExplicitCommands()
    {
        var firstContext = CreateContext(
            Path.Combine(_root, "first"),
            "First.Web",
            Path.Combine(_root, "first", "abpdev.yml"));
        var secondContext = CreateContext(Path.Combine(_root, "second"), "Second.Web");
        _runnerClient.ListAsync(null, false, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RunnerResponse?>(new RunnerResponse
            {
                Contexts = new[] { firstContext, secondContext }
            }));
        var command = new StopCommand(_runnerClient, _contextResolver)
        {
            Projects = new[] { "api" }
        };
        var console = new TestConsole();

        var exception = await Should.ThrowAsync<CommandException>(
            () => command.ExecuteAsync(console).AsTask());

        exception.ExitCode.ShouldBe(1);
        exception.ShowHelp.ShouldBeTrue();
        exception.Message.ShouldContain("No active context matches the current directory");
        exception.Message.ShouldContain(firstContext.WorkingDirectory);
        exception.Message.ShouldContain(secondContext.WorkingDirectory);
        exception.Message.ShouldContain("--yml");
        exception.Message.ShouldContain("--projects");
        exception.Message.ShouldContain("api");
        await _runnerClient.DidNotReceiveWithAnyArgs().StopAsync(default!, default!, default);
    }

    [Fact]
    public async Task Stop_WithoutLocalContext_InteractiveUsesTheSelectedContext()
    {
        var firstContext = CreateContext(Path.Combine(_root, "first"), "First.Web");
        var selectedContext = CreateContext(Path.Combine(_root, "selected"), "Selected.Web");
        _runnerClient.ListAsync(null, false, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RunnerResponse?>(new RunnerResponse
            {
                Contexts = new[] { firstContext, selectedContext }
            }));
        _runnerClient.StopAsync(
                selectedContext.ContextKey,
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RunnerResponse?>(new RunnerResponse
            {
                AffectedCount = 1,
                Messages = new[] { "Stopped 1 application(s)." }
            }));
        var selectionWasRequested = false;
        var command = new StopCommand(
            _runnerClient,
            _contextResolver,
            _ => true,
            contexts =>
            {
                selectionWasRequested = true;
                contexts.ShouldContain(selectedContext);
                return selectedContext;
            },
            _ => throw new InvalidOperationException("Global confirmation should not be requested."));
        var console = new TestConsole();

        await command.ExecuteAsync(console);

        selectionWasRequested.ShouldBeTrue();
        await _runnerClient.Received(1).StopAsync(
            selectedContext.ContextKey,
            Arg.Is<string[]>(x => x.Length == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StopAll_NonInteractiveRequiresForceBeforeReadingRunnerState()
    {
        var command = new StopCommand(_runnerClient, _contextResolver)
        {
            All = true
        };
        var console = new TestConsole();

        var exception = await Should.ThrowAsync<CommandException>(
            () => command.ExecuteAsync(console).AsTask());

        exception.ExitCode.ShouldBe(1);
        exception.ShowHelp.ShouldBeTrue();
        exception.Message.ShouldContain("abpdev stop --all --force");
        await _runnerClient.DidNotReceiveWithAnyArgs().ListAsync(default, default, default);
        await _runnerClient.DidNotReceiveWithAnyArgs().StopAsync(default!, default!, default);
    }

    [Fact]
    public async Task StopAll_WithForceStopsEveryActiveContext()
    {
        var firstContext = CreateContext(Path.Combine(_root, "first"), "First.Web");
        var secondContext = CreateContext(Path.Combine(_root, "second"), "Second.Web");
        _runnerClient.ListAsync(null, false, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RunnerResponse?>(new RunnerResponse
            {
                Contexts = new[] { firstContext, secondContext }
            }));
        _runnerClient.StopAsync(
                firstContext.ContextKey,
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RunnerResponse?>(new RunnerResponse
            {
                AffectedCount = 1,
                Messages = new[] { "Stopped 1 application(s)." }
            }));
        _runnerClient.StopAsync(
                secondContext.ContextKey,
                Arg.Any<string[]>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RunnerResponse?>(new RunnerResponse
            {
                AffectedCount = 1,
                Messages = new[] { "Stopped 1 application(s)." }
            }));
        var command = new StopCommand(_runnerClient, _contextResolver)
        {
            All = true,
            Force = true
        };
        var console = new TestConsole();

        await command.ExecuteAsync(console);

        await _runnerClient.Received(1).StopAsync(
            firstContext.ContextKey,
            Arg.Is<string[]>(x => x.Length == 0),
            Arg.Any<CancellationToken>());
        await _runnerClient.Received(1).StopAsync(
            secondContext.ContextKey,
            Arg.Is<string[]>(x => x.Length == 0),
            Arg.Any<CancellationToken>());
        console.GetOutput().ShouldContain("Stopped 2 application(s) across 2 context(s).");
    }

    [Fact]
    public async Task StopAll_InteractiveCancellationStopsNothing()
    {
        var context = CreateContext(Path.Combine(_root, "active"), "Active.Web");
        _runnerClient.ListAsync(null, false, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RunnerResponse?>(new RunnerResponse
            {
                Contexts = new[] { context }
            }));
        string? confirmationPrompt = null;
        var command = new StopCommand(
            _runnerClient,
            _contextResolver,
            _ => true,
            contexts => contexts[0],
            prompt =>
            {
                confirmationPrompt = prompt;
                return false;
            })
        {
            All = true
        };
        var console = new TestConsole();

        await command.ExecuteAsync(console);

        confirmationPrompt.ShouldNotBeNull();
        confirmationPrompt!.ShouldContain("1 active application(s)");
        confirmationPrompt.ShouldContain("1 context(s)");
        await _runnerClient.DidNotReceiveWithAnyArgs().StopAsync(default!, default!, default);
        console.GetOutput().ShouldContain("No applications were stopped.");
    }

    [Fact]
    public async Task StopAll_RejectsProjectSelectors()
    {
        var command = new StopCommand(_runnerClient, _contextResolver)
        {
            All = true,
            Force = true,
            Projects = new[] { "api" }
        };

        var exception = await Should.ThrowAsync<CommandException>(
            () => command.ExecuteAsync(new TestConsole()).AsTask());

        exception.ShowHelp.ShouldBeTrue();
        exception.Message.ShouldContain("cannot be combined with --projects");
        await _runnerClient.DidNotReceiveWithAnyArgs().ListAsync(default, default, default);
    }

    [Fact]
    public async Task StopForce_WithoutAllShowsHelpBeforeReadingRunnerState()
    {
        var command = new StopCommand(_runnerClient, _contextResolver)
        {
            Force = true
        };

        var exception = await Should.ThrowAsync<CommandException>(
            () => command.ExecuteAsync(new TestConsole()).AsTask());

        exception.ShowHelp.ShouldBeTrue();
        exception.Message.ShouldContain("can only be used together with '--all'");
        await _runnerClient.DidNotReceiveWithAnyArgs().ListAsync(default, default, default);
        await _runnerClient.DidNotReceiveWithAnyArgs().StopAsync(default!, default!, default);
    }

    [Fact]
    public async Task Ps_ListsAllActiveContextsByDefault()
    {
        _runnerClient.ListAsync(null, false, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RunnerResponse?>(new RunnerResponse()));
        var command = new PsCommand(_runnerClient, _contextResolver);
        var console = new TestConsole();

        await command.ExecuteAsync(console);

        await _runnerClient.Received(1).ListAsync(null, false, Arg.Any<CancellationToken>());
        console.GetOutput().ShouldContain("No applications are currently managed");
    }

    [Fact]
    public async Task Attach_LeavesApplicationsRunningWhenTheDashboardDetaches()
    {
        var descriptor = RunnerContextIdentity.Create(_root, null);
        var context = new RunnerContextSnapshot
        {
            ContextKey = descriptor.ContextKey,
            DisplayName = descriptor.DisplayName,
            WorkingDirectory = descriptor.WorkingDirectory,
            Applications = new[]
            {
                new RunnerApplicationSnapshot
                {
                    Id = "app",
                    DisplayName = "Sample.Web",
                    State = RunnerApplicationState.Running
                }
            }
        };
        _runnerClient.ListAsync(descriptor.ContextKey, false, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RunnerResponse?>(new RunnerResponse { Contexts = new[] { context } }));
        _dashboard.RunAsync(descriptor.ContextKey, Arg.Any<IConsole>(), Arg.Any<CancellationToken>())
            .Returns(RunnerDashboardResult.Detached);
        var command = new AttachCommand(
            _runnerClient,
            _contextResolver,
            _dashboard,
            _ => true)
        {
            WorkingDirectory = _root
        };
        var console = new TestConsole();

        await command.ExecuteAsync(console);

        await _dashboard.Received(1).RunAsync(
            descriptor.ContextKey,
            console,
            Arg.Any<CancellationToken>());
        await _runnerClient.DidNotReceiveWithAnyArgs().StopAsync(default!, default!, default);
        console.GetOutput().ShouldContain("Applications continue running in the background.");
        console.GetOutput().ShouldContain("Run 'abpdev attach' to reopen the dashboard.");
    }

    [Fact]
    public async Task Attach_UsesTheNearestAncestorContextBeforePrompting()
    {
        var requestedContext = _contextResolver.Resolve(null);
        var parentDirectory = Directory.GetParent(requestedContext.WorkingDirectory)!.FullName;
        var ancestorContext = CreateContext(parentDirectory, "Ancestor.Web");
        var unrelatedContext = CreateContext(Path.Combine(_root, "unrelated"), "Other.Web");
        _runnerClient.ListAsync(
                requestedContext.ContextKey,
                false,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RunnerResponse?>(new RunnerResponse()));
        _runnerClient.ListAsync(null, false, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RunnerResponse?>(new RunnerResponse
            {
                Contexts = new[] { unrelatedContext, ancestorContext }
            }));
        _dashboard.RunAsync(
                ancestorContext.ContextKey,
                Arg.Any<IConsole>(),
                Arg.Any<CancellationToken>())
            .Returns(RunnerDashboardResult.Detached);
        var command = new AttachCommand(
            _runnerClient,
            _contextResolver,
            _dashboard,
            _ => true);
        var console = new TestConsole();

        await command.ExecuteAsync(console);

        await _dashboard.Received(1).RunAsync(
            ancestorContext.ContextKey,
            console,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Attach_FailsFastWithHelpWhenTheConsoleIsNonInteractive()
    {
        var command = new AttachCommand(_runnerClient, _contextResolver, _dashboard);
        var console = new TestConsole();

        var exception = await Should.ThrowAsync<CommandException>(
            () => command.ExecuteAsync(console).AsTask());

        exception.ExitCode.ShouldBe(1);
        exception.ShowHelp.ShouldBeTrue();
        exception.Message.ShouldContain("requires an interactive terminal");
        exception.Message.ShouldContain("abpdev ps --json");
        exception.Message.ShouldContain("abpdev logs --managed");
        await _runnerClient.DidNotReceiveWithAnyArgs().ListAsync(default, default, default);
        await _dashboard.DidNotReceiveWithAnyArgs().RunAsync(default!, default!, default);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static RunnerContextSnapshot CreateContext(
        string workingDirectory,
        string applicationName,
        string? configurationPath = null)
    {
        var descriptor = RunnerContextIdentity.Create(workingDirectory, configurationPath);
        return new RunnerContextSnapshot
        {
            ContextKey = descriptor.ContextKey,
            DisplayName = descriptor.DisplayName,
            WorkingDirectory = descriptor.WorkingDirectory,
            ConfigurationPath = descriptor.ConfigurationPath,
            ConfigurationHash = descriptor.ConfigurationHash,
            Applications = new[]
            {
                new RunnerApplicationSnapshot
                {
                    Id = applicationName,
                    Name = applicationName,
                    DisplayName = applicationName,
                    State = RunnerApplicationState.Running
                }
            }
        };
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
}
