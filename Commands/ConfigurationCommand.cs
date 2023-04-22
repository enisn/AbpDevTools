using AbpDevTools.Configuration;
using CliFx.Infrastructure;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AbpDevTools.Commands;

public abstract class ConfigurationBasecommand : ICommand
{
    protected abstract string FilePath { get; }

    public virtual ValueTask ExecuteAsync(IConsole console)
    {
        console.Output.WriteLine("Opening file " + FilePath);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo("explorer", FilePath));
        }
        else
        {
            Process.Start(new ProcessStartInfo("open", FilePath));
        }
        return ValueTask.CompletedTask;
    }
}

[Command("replace config", Description = "Allows managing replacement configuration.")]
public class ReplaceConfigurationCommand : ConfigurationBasecommand
{
    protected override string FilePath => ReplacementConfiguration.FilePath;
    public override ValueTask ExecuteAsync(IConsole console)
    {
        ReplacementConfiguration.GetOptions();

        return base.ExecuteAsync(console);
    }
}

[Command("envapp config", Description = "Allows managing replacement configuration.")]
public class EnvironmentAppConfigurationCommand : ConfigurationBasecommand
{
    protected override string FilePath => EnvironmentAppConfiguration.FilePath;
    public override ValueTask ExecuteAsync(IConsole console)
    {
        EnvironmentAppConfiguration.GetOptions();
        return base.ExecuteAsync(console);
    }
}

[Command("run config")]
public class RunConfigurationCommand : ConfigurationBasecommand
{
    protected override string FilePath => RunConfiguration.FilePath;

    public override ValueTask ExecuteAsync(IConsole console)
    {
        RunConfiguration.GetOptions();
        return base.ExecuteAsync(console);
    }
}
