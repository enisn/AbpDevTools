using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AbpDevTools.Configuration;
using CliFx.Infrastructure;

namespace AbpDevTools.Commands.References;

[Command("references", Description = "Allows managing local sources configuration for switch references to local or remote sources.")]
public class ReferencesCommand : ICommand
{
    public async ValueTask ExecuteAsync(IConsole console)
    {
        await console.Output.WriteLineAsync("Local sources are used to switch references to ProjectReference or PackageReference.");
        await console.Output.WriteLineAsync("Configure local sources to used in this command use following command: \n\n abpdev references config\n");

        await console.Output.WriteLineAsync("Usage: \n\n abpdev references to-local \n abpdev references to-package");
    }
}