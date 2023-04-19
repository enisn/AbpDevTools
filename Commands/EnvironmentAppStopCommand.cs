using AbpDevTools.Configuration;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using System.Diagnostics;

namespace AbpDevTools.Commands;

[Command("envapp stop", Description = "Stops previously deployed environment app.")]
public class EnvironmentAppStopCommand : ICommand
{
    [CommandParameter(0, IsRequired = false , Description = "Name of the app.")]
    public string AppName { get; set; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var configurations = EnvironmentToolConfiguration.GetOptions();

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

        if (option.StopCmd.StartsWith("docker "))
        {
            await StopWithCmdAsync(option, cancellationToken);
        }
        else
        {
            throw new CommandException($"Only docker apps supported currently. Your command can't be executed. \n'{option.StartCmd}'\n");
        }
    }

    private async Task StopWithCmdAsync(EnvironmentToolOption option, CancellationToken cancellationToken)
    {
        var commands = option.StopCmd.Split(';');
        foreach (var command in commands)
        {
            var process = Process.Start(
                "docker",
                command.Replace("docker ", string.Empty));

            await process.WaitForExitAsync(cancellationToken);
        }
    }
}
