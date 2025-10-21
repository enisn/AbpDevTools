using AbpDevTools.Configuration;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using System.Diagnostics;

namespace AbpDevTools.Commands;

[Command("envapp stop", Description = "Stops previously deployed environment app.")]
public class EnvironmentAppStopCommand : ICommand
{
    private readonly EnvironmentAppConfiguration environmentAppConfiguration;

    [CommandParameter(0, IsRequired = false , Description = "Name of the app.")]
    public string? AppName { get; set; }

    public EnvironmentAppStopCommand(EnvironmentAppConfiguration environmentAppConfiguration)
    {
        this.environmentAppConfiguration = environmentAppConfiguration;
    }
    public async ValueTask ExecuteAsync(IConsole console)
    {
        var configurations = environmentAppConfiguration.GetOptions();

        if (string.IsNullOrEmpty(AppName))
        {
            console.Output.WriteLine("You must specify an app to run.\n" +
                "envapp start <ToolName>\n" +
                "Available app names:\n - all\n" + string.Join("\n - ", configurations.Keys));
            return;
        }

        var cancellationToken = console.RegisterCancellationHandler();

        if (AppName.Equals("all", StringComparison.InvariantCultureIgnoreCase))
        {
            foreach (var config in configurations) 
            {
                await console.Output.WriteLineAsync($"Stopping {config.Key}...");

                await StopWithCmdAsync(config.Value, cancellationToken);

                await console.Output.WriteLineAsync($"Stopped {config.Key}.");
            }

            return;
        }

        if (!configurations.TryGetValue(AppName, out var option))
        {
            throw new CommandException("App name couldn't be recognized. Try one of them: \n" + string.Join("\n - ", configurations.Keys));
        }

        if (option.StopCmds.Length > 0 && option.StopCmds[0].StartsWith("docker "))
        {
            await StopWithCmdAsync(option, cancellationToken);
        }
        else
        {
            throw new CommandException($"Only docker apps supported currently. Your command can't be executed. \n'{string.Join("; ", option.StartCmds)}'\n");
        }
    }

    private async Task StopWithCmdAsync(EnvironmentToolOption option, CancellationToken cancellationToken)
    {
        foreach (var command in option.StopCmds)
        {
            var process = Process.Start(
                "docker",
                command.Replace("docker ", string.Empty));

            await process.WaitForExitAsync(cancellationToken);
        }
    }
}
