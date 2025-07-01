using AbpDevTools.Configuration;
using CliFx.Exceptions;
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
        await console.Output.WriteLineAsync("-abpdev local-sources config");
        await console.Output.WriteLineAsync("-abpdev config clear  | Resets all the configurations to defaults.");
    }
}

public abstract class ConfigurationBaseCommand<TConfiguration> : ICommand
    where TConfiguration : IConfigurationBase
{
    public TConfiguration Configuration { get; }

    public ConfigurationBaseCommand(TConfiguration configuration)
    {
        Configuration = configuration;
    }

    public virtual ValueTask ExecuteAsync(IConsole console)
    {
        console.Output.WriteLine("Opening file " + Configuration.FilePath);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo("explorer", Configuration.FilePath));
        }
        else
        {
            Process.Start(new ProcessStartInfo("open", $"\"{Configuration.FilePath}\""));
        }
        return ValueTask.CompletedTask;
    }
}

[Command("replace config", Description = "Allows managing replacement configuration.")]
public class ReplaceConfigurationCommand : ConfigurationBaseCommand<ReplacementConfiguration>
{
    public ReplaceConfigurationCommand(ReplacementConfiguration configuration) : base(configuration)
    {
    }

    public override ValueTask ExecuteAsync(IConsole console)
    {
        Configuration.GetOptions();
        return base.ExecuteAsync(console);
    }
}

[Command("envapp config", Description = "Allows managing replacement configuration.")]
public class EnvironmentAppConfigurationCommand : ConfigurationBaseCommand<EnvironmentAppConfiguration>
{
    public EnvironmentAppConfigurationCommand(EnvironmentAppConfiguration configuration) : base(configuration)
    {
    }

    public override ValueTask ExecuteAsync(IConsole console)
    {
        Configuration.GetOptions();
        return base.ExecuteAsync(console);
    }
}

[Command("run config")] [Obsolete]
public class RunConfigurationCommand : ConfigurationBaseCommand<RunConfiguration>
{
    public RunConfigurationCommand(RunConfiguration configuration) : base(configuration)
    {
    }

    public override ValueTask ExecuteAsync(IConsole console)
    {
        Configuration.GetOptions();

        // This command is deprecated.
        // TODO: Remove this command in the future.
        throw new CommandException("This command is deprecated. Use \"abpdev run\" directly instead.");
    }
}

[Command("clean config")]
public class CleanConfigurationCommand : ConfigurationBaseCommand<CleanConfiguration>
{
    public CleanConfigurationCommand(CleanConfiguration configuration) : base(configuration)
    {
    }

    public override ValueTask ExecuteAsync(IConsole console)
    {
        Configuration.GetOptions();
        return base.ExecuteAsync(console);
    }
}

[Command("tools config")]
public class ToolsConfigurationCommand : ConfigurationBaseCommand<ToolsConfiguration>
{
    public ToolsConfigurationCommand(ToolsConfiguration configuration) : base(configuration)
    {
    }

    public override ValueTask ExecuteAsync(IConsole console)
    {
        Configuration.GetOptions();
        return base.ExecuteAsync(console);
    }
}

[Command("local-sources config", Description = "Allows managing local sources configuration.")]
public class LocalSourcesConfigurationCommand : ConfigurationBaseCommand<LocalSourcesConfiguration>
{
    public LocalSourcesConfigurationCommand(LocalSourcesConfiguration configuration) : base(configuration)
    {
    }

    public override ValueTask ExecuteAsync(IConsole console)
    {
        Configuration.GetOptions();
        return base.ExecuteAsync(console);
    }
}