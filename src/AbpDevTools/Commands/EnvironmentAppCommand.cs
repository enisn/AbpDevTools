using AbpDevTools.Configuration;
using CliFx.Infrastructure;

namespace AbpDevTools.Commands;

[Command("envapp", Description = "Environment apps that required while development.")]
public class EnvironmentAppCommand : ICommand
{
    private readonly EnvironmentAppConfiguration environmentAppConfiguration;

    public EnvironmentAppCommand(EnvironmentAppConfiguration environmentAppConfiguration)
    {
        this.environmentAppConfiguration = environmentAppConfiguration;
    }

    public ValueTask ExecuteAsync(IConsole console)
    {
        var options = environmentAppConfiguration.GetOptions();

        console.Output.WriteLine("Available env apps:\n - " + string.Join("\n - ", options.Keys));

        console.Output.WriteLine("\nRunning env app: \n envapp start <app-name>\n envapp start redis");
        console.Output.WriteLine("\nStopping env app: \n envapp stop <app-name>\n envapp stop redis");
        console.Output.WriteLine("\nEditing env apps: \n envapp config");

        return ValueTask.CompletedTask;
    }
}
