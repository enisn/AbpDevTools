using AbpDevTools.Configuration;
using CliFx.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AbpDevTools.Commands;

[Command("env config")]
public class EnvironmentConfigurationCommand : ConfigurationBaseCommand
{
    protected override string FilePath => EnvironmentConfiguration.FilePath;
    public override ValueTask ExecuteAsync(IConsole console)
    {
        EnvironmentConfiguration.GetOptions();

        return base.ExecuteAsync(console);
    }
}
