using AbpDevTools.Configuration;
using CliFx.Infrastructure;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AbpDevTools.Commands;

[Command("config", Description = "Allows managing configuration.")]
public class ConfigurationCommand : ICommand
{
    public ValueTask ExecuteAsync(IConsole console)
    {
        ReplacementConfiguration.GetOptions();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo("notepad", ReplacementConfiguration.FilePath));
        }
        else
        {
            Process.Start(new ProcessStartInfo("/bin/bash", "open " + ReplacementConfiguration.FilePath));
        }
        return ValueTask.CompletedTask;
    }
}
