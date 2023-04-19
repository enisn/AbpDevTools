using AbpDevTools.Configuration;
using CliFx.Infrastructure;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AbpDevTools.Commands;

[Command("replace config", Description = "Allows managing replacement configuration.")]
public class ReplaceConfigurationCommand : ICommand
{
    public ValueTask ExecuteAsync(IConsole console)
    {
        ReplacementConfiguration.GetOptions();

        console.Output.WriteLine("Opening file " + ReplacementConfiguration.FilePath);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo("explorer", ReplacementConfiguration.FilePath));
        }
        else
        {
            Process.Start(new ProcessStartInfo("open", ReplacementConfiguration.FilePath));
        }
        return ValueTask.CompletedTask;
    }
}

[Command("envapp config", Description = "Allows managing replacement configuration.")]
public class ConfigurationCommand : ICommand
{
    public ValueTask ExecuteAsync(IConsole console)
    {
        EnvironmentToolConfiguration.GetOptions();

        console.Output.WriteLine("Opening file " + EnvironmentToolConfiguration.FilePath);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo("explorer", EnvironmentToolConfiguration.FilePath));
        }
        else
        {
            Process.Start(new ProcessStartInfo("open", EnvironmentToolConfiguration.FilePath));
        }
        return ValueTask.CompletedTask;
    }
}
