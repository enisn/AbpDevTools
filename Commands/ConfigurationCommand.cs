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
            System.Console.WriteLine(ReplacementConfiguration.FilePath);
            Process.Start(new ProcessStartInfo("open", ReplacementConfiguration.FilePath));
        }
        return ValueTask.CompletedTask;
    }
}
