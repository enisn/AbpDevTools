using AbpDevTools.Configuration;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using System.Diagnostics;

namespace AbpDevTools.Commands;
[Command("envapp start", Description = "Deploys infrastructural tools to docker. Such as Redis, RabbitMQ, SqlServer etc.")]
public class EnvironmentAppStartCommand : ICommand
{
    [CommandParameter(0, IsRequired = false, Description = "Name of the app.")]
    public string[] AppNames { get; set; }

    [CommandOption("password", 'p', Description = "Default password for sql images when applicable. Default: 12345678Aa")]
    public string DefaultPassword { get; set; }

    protected IConsole console;
    protected Dictionary<string,EnvironmentToolOption> configurations;

    public async ValueTask ExecuteAsync(IConsole console)
    {
        this.console = console;
        configurations = EnvironmentAppConfiguration.GetOptions();

        if (AppNames == null || AppNames.Length == 0)
        {
            console.Output.WriteLine("You must specify an app to run.\n" +
                "envapp start <ToolName>\n" +
                "Available app names:\n - " + string.Join("\n - ", configurations.Keys));

            return;
        }

        foreach (var appName in AppNames)
        {
            await StartAppAsync(appName);
        }
    }

    protected async Task StartAppAsync(string appName)
    {
        if (string.IsNullOrEmpty(appName))
        {
            await console.Output.WriteAsync("App Name can't be null or empty.");
            return;
        }

        if (!configurations.TryGetValue(appName, out var option))
        {
            throw new CommandException("ToolName couldn't be recognized. Try one of them: \n - " + string.Join("\n - ", configurations.Keys));
        }

        if (string.IsNullOrEmpty(DefaultPassword))
        {
            DefaultPassword = "12345678Aa";
        }

        await RunCommandAsync(option.StartCmd.Replace("Passw0rd", DefaultPassword));
    }

    protected async Task RunCommandAsync(string command)
    {
        var commands = command.Split(';');
        foreach (var c in commands)
        {
            var fileName = c[..c.IndexOf(' ')];

            var process = Process.Start(fileName, c[c.IndexOf(' ')..]);
            await process.WaitForExitAsync();
        }
    }
}
