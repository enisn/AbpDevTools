using AbpDevTools.Runner;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using Spectre.Console;
using System.Text.Json;

namespace AbpDevTools.Commands;

[Command("ps", Description = "List applications managed by the centralized runner")]
public sealed class PsCommand : ICommand
{
    private readonly IRunnerClient _runnerClient;
    private readonly RunnerContextResolver _contextResolver;

    public PsCommand(IRunnerClient runnerClient, RunnerContextResolver contextResolver)
    {
        _runnerClient = runnerClient;
        _contextResolver = contextResolver;
    }

    [CommandParameter(0, IsRequired = false, Description = "Limit results to a working directory. By default, all active contexts are listed.")]
    public string? WorkingDirectory { get; set; }

    [CommandOption("current", Description = "Limit results to the current working directory and its abpdev.yml context.")]
    public bool Current { get; set; }

    [CommandOption("all", 'a', Description = "Include applications that have exited or were stopped.")]
    public bool IncludeInactive { get; set; }

    [CommandOption("yml", Description = "Exact abpdev.yml path used to identify a directory-scoped context.")]
    public string? YmlPath { get; set; }

    [CommandOption("json", Description = "Write the runner state as JSON.")]
    public bool Json { get; set; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var cancellationToken = console.RegisterCancellationHandler();
        string? contextKey = null;
        if (!string.IsNullOrWhiteSpace(WorkingDirectory) || Current || !string.IsNullOrWhiteSpace(YmlPath))
        {
            try
            {
                contextKey = _contextResolver.Resolve(
                    WorkingDirectory ?? Directory.GetCurrentDirectory(),
                    YmlPath).ContextKey;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                throw new CommandException(ex.Message);
            }
        }

        var response = await _runnerClient.ListAsync(contextKey, IncludeInactive, cancellationToken);
        if (response is null)
        {
            await console.Output.WriteLineAsync("The centralized runner is not running.");
            return;
        }

        if (!response.Success)
        {
            throw new CommandException(response.Error ?? "Unable to read centralized runner state.");
        }

        if (Json)
        {
            await console.Output.WriteLineAsync(JsonSerializer.Serialize(
                response.Contexts,
                new JsonSerializerOptions(RunnerProtocol.JsonOptions) { WriteIndented = true }));
            return;
        }

        if (response.Contexts.Length == 0)
        {
            await console.Output.WriteLineAsync(
                contextKey is null
                    ? "No applications are currently managed by abpdev."
                    : "No managed applications were found for this directory and YAML context.");
            return;
        }

        if (!ConsoleSupport.SupportsInteractiveConsole(console))
        {
            foreach (var context in response.Contexts)
            {
                await console.Output.WriteLineAsync($"{context.DisplayName} ({context.WorkingDirectory})");
                foreach (var app in context.Applications)
                {
                    await console.Output.WriteLineAsync(
                        $"- {app.DisplayName}: {app.State}; readiness={app.Readiness}; pid={app.ProcessId?.ToString() ?? "-"}; {app.Status}");
                }
            }

            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("abpdev managed applications")
            .AddColumn("Context")
            .AddColumn("Application")
            .AddColumn("State")
            .AddColumn("Ready")
            .AddColumn("PID")
            .AddColumn("Endpoint / status");

        foreach (var context in response.Contexts)
        {
            foreach (var app in context.Applications)
            {
                table.AddRow(
                    Markup.Escape(context.DisplayName),
                    Markup.Escape(app.DisplayName),
                    Markup.Escape(app.State.ToString()),
                    Markup.Escape(app.Readiness.ToString()),
                    app.ProcessId?.ToString() ?? "-",
                    Markup.Escape(app.Url ?? app.Status));
            }
        }

        AnsiConsole.Write(table);
    }
}
