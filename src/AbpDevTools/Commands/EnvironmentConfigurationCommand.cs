using AbpDevTools.Configuration;
using CliFx.Infrastructure;

namespace AbpDevTools.Commands;

[Command("env config")]
public class EnvironmentConfigurationCommand : ConfigurationBaseCommand<EnvironmentConfiguration>
{
    public EnvironmentConfigurationCommand(EnvironmentConfiguration configuration) : base(configuration)
    {
    }

    public override ValueTask ExecuteAsync(IConsole console)
    {
        Configuration.GetOptions();
        return base.ExecuteAsync(console);
    }
}
