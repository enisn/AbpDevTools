using AbpDevTools.Configuration;
using CliFx.Infrastructure;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AbpDevTools.Commands;

[Command("config")]
public class ConfigCommand : ICommand
{
    public async ValueTask ExecuteAsync(IConsole console)
    {
        await console.Output.WriteLineAsync("Available commands:\n");
        await console.Output.WriteLineAsync("-abpdev replace config");
        await console.Output.WriteLineAsync("-abpdev envapp config");
        await console.Output.WriteLineAsync("-abpdev run config");
        await console.Output.WriteLineAsync("-abpdev clean config");
        await console.Output.WriteLineAsync("-abpdev tools config");
        await console.Output.WriteLineAsync("-abpdev config clear  | Resets all the configurations to defaults.");
    }
}

public abstract class ConfigurationBaseCommand : ICommand
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
            Process.Start(new ProcessStartInfo("open", $"\"{FilePath}\""));
        }
        return ValueTask.CompletedTask;
    }
}

[Command("replace config", Description = "Allows managing replacement configuration.")]
public class ReplaceConfigurationCommand : ConfigurationBaseCommand
{
    protected override string FilePath => ReplacementConfiguration.FilePath;
    public override ValueTask ExecuteAsync(IConsole console)
    {
        ReplacementConfiguration.GetOptions();

        return base.ExecuteAsync(console);
    }
}

[Command("envapp config", Description = "Allows managing replacement configuration.")]
public class EnvironmentAppConfigurationCommand : ConfigurationBaseCommand
{
    protected override string FilePath => EnvironmentAppConfiguration.FilePath;
    public override ValueTask ExecuteAsync(IConsole console)
    {
        EnvironmentAppConfiguration.GetOptions();
        return base.ExecuteAsync(console);
    }
}

[Command("run config")]
public class RunConfigurationCommand : ConfigurationBaseCommand
{
    protected override string FilePath => RunConfiguration.FilePath;

    public override ValueTask ExecuteAsync(IConsole console)
    {
        RunConfiguration.GetOptions();
        return base.ExecuteAsync(console);
    }
}

[Command("clean config")]
public class CleanConfigurationCommand : ConfigurationBaseCommand
{
    protected override string FilePath => CleanConfiguration.FilePath;

    public override ValueTask ExecuteAsync(IConsole console)
    {
        CleanConfiguration.GetOptions();
        return base.ExecuteAsync(console);
    }
}

[Command("tools config")]
public class ToolsConfigurationCommand : ConfigurationBaseCommand
{
    protected override string FilePath => ToolsConfiguration.FilePath;

    public override ValueTask ExecuteAsync(IConsole console)
    {
        ToolsConfiguration.GetOptions();
        return base.ExecuteAsync(console);
    }
}