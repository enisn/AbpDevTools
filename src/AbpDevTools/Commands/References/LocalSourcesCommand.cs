using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AbpDevTools.Configuration;
using CliFx.Infrastructure;

namespace AbpDevTools.Commands.References;

[Command("local-sources config", Description = "Allows managing local sources configuration.")]
public class LocalSourcesCommand : ConfigurationBaseCommand<LocalSourcesConfiguration>
{
    public LocalSourcesCommand(LocalSourcesConfiguration configuration) : base(configuration)
    {
    }

    public override ValueTask ExecuteAsync(IConsole console)
    {
        Configuration.GetOptions();
        return base.ExecuteAsync(console);
    }
}