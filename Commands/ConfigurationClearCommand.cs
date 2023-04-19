using AbpDevTools.Configuration;
using CliFx.Infrastructure;

namespace AbpDevTools.Commands;

public abstract class ConfigurationClearCommandBase : ICommand
{
    [CommandOption("force", 'f')]
    public bool Force { get; set; }

    protected abstract string FilePath { get; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (!Force)
        {
            await console
                .Output
                .WriteAsync($"Are you sure to remove existing configuration at path {FilePath}?\nY/N?");

            var confirm = await console.Input.ReadLineAsync();
            if (!confirm.Equals("Y", StringComparison.InvariantCultureIgnoreCase))
            {
                return;
            }
        }

        File.Delete(FilePath);
    }
}

[Command("config clear", Description = "Clears the current configuration.")]
public class ConfigurationClearCommand : ICommand
{
    [CommandOption("force", 'f')]
    public bool Force { get; set; }

    protected readonly ConfigurationClearCommandBase[] configurationClearCommands = new ConfigurationClearCommandBase[]
    {
        new ReplacementConfigClearCommand(),
        new EnvironmentAppConfigClearCommand()
    };

    public async ValueTask ExecuteAsync(IConsole console)
    {
        foreach (var command in configurationClearCommands)
        {
            command.Force = Force;
            await command.ExecuteAsync(console);
        }
    }
}

[Command("replace config clear")]
public class ReplacementConfigClearCommand : ConfigurationClearCommandBase
{
    protected override string FilePath => ReplacementConfiguration.FilePath;
}

[Command("envapp config clear")]
public class EnvironmentAppConfigClearCommand : ConfigurationClearCommandBase
{
    protected override string FilePath => EnvironmentAppConfiguration.FilePath;
}