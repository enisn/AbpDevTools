using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AbpDevTools.Configuration;
using CliFx.Infrastructure;

namespace AbpDevTools.Commands.References;

[Command("local-sources", Description = "Allows managing local sources configuration.")]
public class LocalSourcesCommand : ConfigurationBaseCommand<LocalSourcesConfiguration>
{
    public LocalSourcesCommand(LocalSourcesConfiguration configuration) : base(configuration)
    {
    }

    public override async ValueTask ExecuteAsync(IConsole console)
    {
        await console.Output.WriteLineAsync("Local sources are used to add local packages to the project as ProjectReference.");
        await console.Output.WriteLineAsync("Configure local sources with this file: " + Configuration.FilePath);
        Configuration.GetOptions();
        await base.ExecuteAsync(console);
    }
}