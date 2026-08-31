using AbpDevTools.Runner;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using Spectre.Console;

namespace AbpDevTools.Commands;

[Command("attach", Description = "Open the interactive dashboard for a centralized-runner context")]
public sealed class AttachCommand : ICommand
{
    private readonly IRunnerClient _runnerClient;
    private readonly RunnerContextResolver _contextResolver;
    private readonly IRunnerDashboard _runnerDashboard;

    public AttachCommand(
        IRunnerClient runnerClient,
        RunnerContextResolver contextResolver,
        IRunnerDashboard runnerDashboard)
    {
        _runnerClient = runnerClient;
        _contextResolver = contextResolver;
        _runnerDashboard = runnerDashboard;
    }

    [CommandParameter(0, IsRequired = false, Description = "Working directory to attach. Default: current directory, or the only active context.")]
    public string? WorkingDirectory { get; set; }

    [CommandOption("yml", Description = "Exact abpdev.yml path used when the context was started.")]
    public string? YmlPath { get; set; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var cancellationToken = console.RegisterCancellationHandler();
        var explicitContext = !string.IsNullOrWhiteSpace(WorkingDirectory) || !string.IsNullOrWhiteSpace(YmlPath);
        RunnerContextDescriptor descriptor;
        try
        {
            descriptor = _contextResolver.Resolve(WorkingDirectory, YmlPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new CommandException(ex.Message);
        }

        var response = await _runnerClient.ListAsync(descriptor.ContextKey, includeInactive: false, cancellationToken);
        if (response is null)
        {
            await console.Output.WriteLineAsync("The centralized runner is not running.");
            return;
        }

        var context = response.Contexts.FirstOrDefault();
        if (context is null && !explicitContext)
        {
            var allContextsResponse = await _runnerClient.ListAsync(includeInactive: false, cancellationToken: cancellationToken);
            var activeContexts = allContextsResponse?.Contexts ?? Array.Empty<RunnerContextSnapshot>();
            if (activeContexts.Length == 1)
            {
                context = activeContexts[0];
            }
            else if (activeContexts.Length > 1 && ConsoleSupport.SupportsInteractiveConsole(console))
            {
                context = AnsiConsole.Prompt(
                    new SelectionPrompt<RunnerContextSnapshot>()
                        .Title("Choose a managed context to attach.")
                        .PageSize(12)
                        .UseConverter(x => $"{x.DisplayName} ({x.WorkingDirectory})")
                        .AddChoices(activeContexts));
            }
        }

        if (context is null)
        {
            throw new CommandException(
                explicitContext
                    ? "No active managed applications were found for this directory and YAML context."
                    : "No active context matches the current directory. Pass a directory shown by 'abpdev ps'.");
        }

        var dashboardResult = await _runnerDashboard.RunAsync(context.ContextKey, console, cancellationToken);
        if (dashboardResult is RunnerDashboardResult.Detached or RunnerDashboardResult.Cancelled)
        {
            await console.Output.WriteLineAsync("Dashboard detached; applications remain managed in the background.");
        }
        else if (dashboardResult == RunnerDashboardResult.Unavailable)
        {
            throw new CommandException("The centralized runner became unavailable.");
        }
        else
        {
            await console.Output.WriteLineAsync("No applications remain active in this context.");
        }
    }
}
