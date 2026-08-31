using AbpDevTools.Runner;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using Spectre.Console;

namespace AbpDevTools.Commands;

[Command("stop", Description = "Stop applications in one or all centralized-runner contexts")]
public sealed class StopCommand : ICommand
{
    private readonly IRunnerClient _runnerClient;
    private readonly RunnerContextResolver _contextResolver;
    private readonly Func<IConsole?, bool> _supportsInteractiveConsole;
    private readonly Func<IReadOnlyList<RunnerContextSnapshot>, RunnerContextSnapshot> _selectContext;
    private readonly Func<string, bool> _confirmStopAll;

    public StopCommand(IRunnerClient runnerClient, RunnerContextResolver contextResolver)
        : this(
            runnerClient,
            contextResolver,
            ConsoleSupport.SupportsInteractiveConsole,
            PromptForContext,
            ConfirmStopAll)
    {
    }

    internal StopCommand(
        IRunnerClient runnerClient,
        RunnerContextResolver contextResolver,
        Func<IConsole?, bool> supportsInteractiveConsole,
        Func<IReadOnlyList<RunnerContextSnapshot>, RunnerContextSnapshot> selectContext,
        Func<string, bool> confirmStopAll)
    {
        _runnerClient = runnerClient;
        _contextResolver = contextResolver;
        _supportsInteractiveConsole = supportsInteractiveConsole;
        _selectContext = selectContext;
        _confirmStopAll = confirmStopAll;
    }

    [CommandParameter(0, IsRequired = false, Description = "Working directory whose managed applications should be stopped. Default: current directory.")]
    public string? WorkingDirectory { get; set; }

    [CommandOption("projects", 'p', Description = "Only stop applications matching these names, paths, or npm scripts.")]
    public string[] Projects { get; set; } = Array.Empty<string>();

    [CommandOption("yml", Description = "Exact abpdev.yml path used when the context was started.")]
    public string? YmlPath { get; set; }

    [CommandOption("all", 'a', Description = "Stop every active application across all centralized-runner contexts.")]
    public bool All { get; set; }

    [CommandOption("force", Description = "Bypass confirmation for --all. Required in non-interactive environments.")]
    public bool Force { get; set; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        ValidateOptions();
        var cancellationToken = console.RegisterCancellationHandler();

        if (All)
        {
            await StopAllContextsAsync(console, cancellationToken);
            return;
        }

        var explicitContext = !string.IsNullOrWhiteSpace(WorkingDirectory) ||
                              !string.IsNullOrWhiteSpace(YmlPath);
        var requestedContext = ResolveContext();
        if (explicitContext)
        {
            await StopContextAsync(requestedContext.ContextKey, Projects, console, cancellationToken);
            return;
        }

        var listResponse = await _runnerClient.ListAsync(
            includeInactive: false,
            cancellationToken: cancellationToken);
        if (listResponse is null)
        {
            await console.Output.WriteLineAsync(
                "The centralized runner is not running; there are no managed applications to stop.");
            return;
        }

        if (!listResponse.Success)
        {
            throw new CommandException(listResponse.Error ?? "Unable to read centralized runner state.");
        }

        var activeContexts = listResponse.Contexts.Where(x => x.IsActive).ToArray();
        if (activeContexts.Length == 0)
        {
            await console.Output.WriteLineAsync("No applications are currently managed by abpdev.");
            return;
        }

        var selectedContext = _contextResolver.FindBestActiveContext(requestedContext, activeContexts);
        if (selectedContext is null)
        {
            if (!_supportsInteractiveConsole(console))
            {
                throw CreateNoMatchingContextException(requestedContext, activeContexts);
            }

            selectedContext = _selectContext(activeContexts);
        }

        await StopContextAsync(selectedContext.ContextKey, Projects, console, cancellationToken);
    }

    private RunnerContextDescriptor ResolveContext()
    {
        try
        {
            return _contextResolver.Resolve(WorkingDirectory, YmlPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new CommandException(ex.Message);
        }
    }

    private void ValidateOptions()
    {
        if (Force && !All)
        {
            throw new CommandException(
                "The '--force' option can only be used together with '--all'.",
                exitCode: 1,
                showHelp: true);
        }

        if (!All)
        {
            return;
        }

        var conflictingOptions = new List<string>();
        if (!string.IsNullOrWhiteSpace(WorkingDirectory))
        {
            conflictingOptions.Add("<workingdirectory>");
        }

        if (!string.IsNullOrWhiteSpace(YmlPath))
        {
            conflictingOptions.Add("--yml");
        }

        if (Projects.Length > 0)
        {
            conflictingOptions.Add("--projects");
        }

        if (conflictingOptions.Count > 0)
        {
            throw new CommandException(
                $"The '--all' option cannot be combined with {string.Join(", ", conflictingOptions)}.",
                exitCode: 1,
                showHelp: true);
        }
    }

    private async Task StopAllContextsAsync(IConsole console, CancellationToken cancellationToken)
    {
        if (!Force && !_supportsInteractiveConsole(console))
        {
            throw new CommandException(
                "Stopping every managed application requires explicit confirmation. " +
                "Use 'abpdev stop --all --force' in a non-interactive environment.",
                exitCode: 1,
                showHelp: true);
        }

        var listResponse = await _runnerClient.ListAsync(
            includeInactive: false,
            cancellationToken: cancellationToken);
        if (listResponse is null)
        {
            await console.Output.WriteLineAsync(
                "The centralized runner is not running; there are no managed applications to stop.");
            return;
        }

        if (!listResponse.Success)
        {
            throw new CommandException(listResponse.Error ?? "Unable to read centralized runner state.");
        }

        var activeContexts = listResponse.Contexts.Where(x => x.IsActive).ToArray();
        var activeApplicationCount = activeContexts.Sum(x => x.Applications.Count(app => app.IsActive));
        if (activeApplicationCount == 0)
        {
            await console.Output.WriteLineAsync("No applications are currently managed by abpdev.");
            return;
        }

        if (!Force && !_confirmStopAll(
                $"Stop all {activeApplicationCount} active application(s) across " +
                $"{activeContexts.Length} context(s)?"))
        {
            await console.Output.WriteLineAsync("No applications were stopped.");
            return;
        }

        var stoppedApplicationCount = 0;
        var completedContextCount = 0;
        var failures = new List<string>();
        foreach (var context in activeContexts)
        {
            var response = await _runnerClient.StopAsync(
                context.ContextKey,
                Array.Empty<string>(),
                cancellationToken);
            if (response is null)
            {
                failures.Add($"{context.DisplayName}: the centralized runner became unavailable");
                break;
            }

            if (!response.Success)
            {
                failures.Add($"{context.DisplayName}: {response.Error ?? "unable to stop applications"}");
                continue;
            }

            stoppedApplicationCount += response.AffectedCount;
            completedContextCount++;
            foreach (var message in response.Messages)
            {
                await console.Output.WriteLineAsync($"[{context.DisplayName}] {message}");
            }
        }

        if (failures.Count > 0)
        {
            throw new CommandException(
                $"Stopped {stoppedApplicationCount} application(s), but the global stop was incomplete:\n- " +
                string.Join("\n- ", failures));
        }

        await console.Output.WriteLineAsync(
            $"Stopped {stoppedApplicationCount} application(s) across {completedContextCount} context(s).");
    }

    private async Task StopContextAsync(
        string contextKey,
        string[] projectFilters,
        IConsole console,
        CancellationToken cancellationToken)
    {
        var response = await _runnerClient.StopAsync(contextKey, projectFilters, cancellationToken);

        if (response is null)
        {
            await console.Output.WriteLineAsync("The centralized runner is not running; there are no managed applications to stop.");
            return;
        }

        if (!response.Success)
        {
            throw new CommandException(response.Error ?? "Unable to stop the managed applications.");
        }

        foreach (var message in response.Messages)
        {
            await console.Output.WriteLineAsync(message);
        }
    }

    private CommandException CreateNoMatchingContextException(
        RunnerContextDescriptor requestedContext,
        IReadOnlyList<RunnerContextSnapshot> activeContexts)
    {
        var availableContexts = string.Join(
            "\n",
            activeContexts.Select(context =>
            {
                var activeCount = context.Applications.Count(x => x.IsActive);
                return $"- {context.DisplayName} ({activeCount} active): {context.WorkingDirectory}\n" +
                       $"  {BuildExplicitStopCommand(context)}";
            }));
        return new CommandException(
            $"No active context matches the current directory '{requestedContext.WorkingDirectory}'.\n" +
            "Pass one of the active context working directories explicitly:\n" +
            availableContexts,
            exitCode: 1,
            showHelp: true);
    }

    private string BuildExplicitStopCommand(RunnerContextSnapshot context)
    {
        var parts = new List<string>
        {
            "abpdev stop",
            QuoteArgument(context.WorkingDirectory)
        };
        if (!string.IsNullOrWhiteSpace(context.ConfigurationPath))
        {
            parts.Add("--yml");
            parts.Add(QuoteArgument(context.ConfigurationPath));
        }

        foreach (var project in Projects)
        {
            parts.Add("--projects");
            parts.Add(QuoteArgument(project));
        }

        return string.Join(" ", parts);
    }

    private static string QuoteArgument(string value)
    {
        if (OperatingSystem.IsWindows())
        {
            return $"\"{value}\"";
        }

        var escapedValue = value.Replace("'", "'\"'\"'");
        return $"'{escapedValue}'";
    }

    private static RunnerContextSnapshot PromptForContext(
        IReadOnlyList<RunnerContextSnapshot> activeContexts)
    {
        return AnsiConsole.Prompt(
            new SelectionPrompt<RunnerContextSnapshot>()
                .Title("Choose a managed context to stop.")
                .PageSize(12)
                .UseConverter(context =>
                    $"{context.DisplayName} ({context.WorkingDirectory}) - " +
                    $"{context.Applications.Count(x => x.IsActive)} active application(s)")
                .AddChoices(activeContexts));
    }

    private static bool ConfirmStopAll(string prompt)
    {
        return AnsiConsole.Prompt(new ConfirmationPrompt(prompt) { DefaultValue = false });
    }
}
