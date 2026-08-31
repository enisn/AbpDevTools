using AbpDevTools.Runner;
using CliFx.Exceptions;
using CliFx.Infrastructure;

namespace AbpDevTools.Commands;

[Command("stop", Description = "Stop applications in the current centralized-runner context")]
public sealed class StopCommand : ICommand
{
    private readonly IRunnerClient _runnerClient;
    private readonly RunnerContextResolver _contextResolver;

    public StopCommand(IRunnerClient runnerClient, RunnerContextResolver contextResolver)
    {
        _runnerClient = runnerClient;
        _contextResolver = contextResolver;
    }

    [CommandParameter(0, IsRequired = false, Description = "Working directory whose managed applications should be stopped. Default: current directory.")]
    public string? WorkingDirectory { get; set; }

    [CommandOption("projects", 'p', Description = "Only stop applications matching these names, paths, or npm scripts.")]
    public string[] Projects { get; set; } = Array.Empty<string>();

    [CommandOption("yml", Description = "Exact abpdev.yml path used when the context was started.")]
    public string? YmlPath { get; set; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var cancellationToken = console.RegisterCancellationHandler();
        RunnerContextDescriptor context;
        try
        {
            context = _contextResolver.Resolve(WorkingDirectory, YmlPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new CommandException(ex.Message);
        }

        var response = await _runnerClient.StopAsync(context.ContextKey, Projects, cancellationToken);
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
}
