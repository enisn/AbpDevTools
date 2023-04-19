using AbpDevTools.Configuration;
using CliFx.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AbpDevTools.Commands;

[Command("config clear", Description = "Clears the current configuration.")]
public class ConfigurationClearCommand : ICommand
{
    [CommandOption("force", 'f')]
    public bool Force { get; set; }
    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (!Force)
        {
            await console
                .Output
                .WriteAsync($"Are you sure to remove existing configuration at path {ReplacementConfiguration.FilePath}?\nY/N?");

            var confirm = await console.Input.ReadLineAsync();
            if (confirm != "Y")
            {
                return;
            }
        }

        ReplacementConfiguration.Remove();
    }
}
