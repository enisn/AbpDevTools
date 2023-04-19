using AbpDevTools.Configuration;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using System.Diagnostics;

namespace AbpDevTools.Commands;
[Command("envapp start", Description = "Deploys infrastructural tools to docker. Such as Redis, RabbitMQ, SqlServer etc.")]
public class EnvironmentAppStartCommand : ICommand
{
    [CommandParameter(0, IsRequired = false , Description = "Name of the app.")]
    public string AppName { get; set; }

    [CommandOption("password", 'p', Description = "Default password for sql images when applicable. Default: 12345678Aa")]
    public string DefaultPassword { get; set; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var configurations = EnvironmentToolConfiguration.GetOptions();

        if (string.IsNullOrEmpty(AppName))
        {
            console.Output.WriteLine("You must specify an app to run.\n" +
                "envapp start <ToolName>\n" +
                "Available app names:\n" + string.Join("\n - ",configurations.Keys));
            return;
        }

        if (!configurations.TryGetValue(AppName, out var option))
        {
            throw new CommandException("ToolName couldn't be recognized. Try one of them: \n" + string.Join("\n - ", configurations.Keys));
        }

        if (string.IsNullOrEmpty(DefaultPassword))
        {
            DefaultPassword = "12345678Aa";
        }

        if (option.StartCmd.StartsWith("docker "))
        {
            var process = Process.Start("docker", option.StartCmd
                .Replace("docker ", string.Empty)
                .Replace("{Password}", DefaultPassword));
            await process.WaitForExitAsync(console.RegisterCancellationHandler());
        }
        else
        {
            throw new CommandException($"Only docker apps supported currently. Your command can't be executed. \n'{option.StartCmd}'\n");
        }
    }
}
