using AbpDevTools.Configuration;
using CliFx.Infrastructure;

namespace AbpDevTools.Commands;

public abstract class ConfigurationClearCommandBase : ICommand
{
    [CommandOption("force", 'f')]
    public bool Force { get; set; }

    public virtual ValueTask ExecuteAsync(IConsole console)
    {
        throw new NotImplementedException();
    }
}

public abstract class ConfigurationClearCommandBase<TConfiguration> : ConfigurationClearCommandBase
    where TConfiguration : IConfigurationBase
{

    private readonly TConfiguration configuration;

    public ConfigurationClearCommandBase(TConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public override async ValueTask ExecuteAsync(IConsole console)
    {
        if (!Force)
        {
            await console
                .Output
                .WriteAsync($"Are you sure to remove existing configuration at path {configuration.FilePath}?\nY/N?");

            var confirm = await console.Input.ReadLineAsync();
            if (!confirm!.Equals("Y", StringComparison.InvariantCultureIgnoreCase))
            {
                return;
            }
        }

        File.Delete(configuration.FilePath);
    }
}

[Command("config clear", Description = "Clears the current configuration.")]
public class ConfigurationClearCommand : ICommand
{
    [CommandOption("force", 'f')]
    public bool Force { get; set; }

    protected readonly ConfigurationClearCommandBase[] configurationClearCommands;

    public ConfigurationClearCommand(
        ReplacementConfigClearCommand replacementConfigClearCommand,
        EnvironmentAppConfigClearCommand environmentAppConfigClearCommand,
        RunConfigClearCommand runConfigClearCommand,
        CleanConfigClearCommand cleanConfigClearCommand,
        ToolsConfigClearCommand toolsConfigClearCommand)
    {
        configurationClearCommands = new ConfigurationClearCommandBase[]
        {
            replacementConfigClearCommand,
            environmentAppConfigClearCommand,
            runConfigClearCommand,
            cleanConfigClearCommand,
            toolsConfigClearCommand,
        };
    }

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
[RegisterTransient]
public class ReplacementConfigClearCommand : ConfigurationClearCommandBase<ReplacementConfiguration>
{
    public ReplacementConfigClearCommand(ReplacementConfiguration configuration) : base(configuration)
    {
    }
}

[Command("envapp config clear")]
public class EnvironmentAppConfigClearCommand : ConfigurationClearCommandBase<EnvironmentAppConfiguration>
{
    public EnvironmentAppConfigClearCommand(EnvironmentAppConfiguration configuration) : base(configuration)
    {
    }
}

[Command("run config clear")]
public class RunConfigClearCommand : ConfigurationClearCommandBase<RunConfiguration>
{
    public RunConfigClearCommand(RunConfiguration configuration) : base(configuration)
    {
    }
}

[Command("clean config clear")]
public class CleanConfigClearCommand : ConfigurationClearCommandBase<CleanConfiguration>
{
    public CleanConfigClearCommand(CleanConfiguration configuration) : base(configuration)
    {
    }
}
[Command("tools config clear")]
public class ToolsConfigClearCommand : ConfigurationClearCommandBase<ToolsConfiguration>
{
    public ToolsConfigClearCommand(ToolsConfiguration configuration) : base(configuration)
    {
    }
}