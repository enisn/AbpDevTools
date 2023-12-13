using AbpDevTools.Configuration;
using AbpDevTools.Environments;
using CliFx.Infrastructure;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AbpDevTools.Commands;

[Command("switch-to-env", Description = "Switches to the specified environment.")]
public class SwitchToEnvironmentCommand : ICommand
{
    protected readonly IProcessEnvironmentManager processEnvironmentManager;
    protected readonly ToolsConfiguration toolsConfiguration;

    [CommandParameter(0, IsRequired = true, Description = "Virtual Environment name to switch in this process")]
    public string? EnvironmentName { get; set; }

    public SwitchToEnvironmentCommand(IProcessEnvironmentManager processEnvironmentManager, ToolsConfiguration toolsConfiguration)
    {
        this.processEnvironmentManager = processEnvironmentManager;
        this.toolsConfiguration = toolsConfiguration;
    }

    public ValueTask ExecuteAsync(IConsole console)
    {
        if (string.IsNullOrEmpty(EnvironmentName))
        {
            throw new ArgumentException("Environment name can not be null or empty.");
        }

        var startInfo = GetStartInfo();

        processEnvironmentManager.SetEnvironmentForProcess(EnvironmentName, startInfo);

        var process = Process.Start(startInfo)!;
        console.Output.WriteAsync($"Switched to {EnvironmentName} on the process (PID: {process.Id} - {process.ProcessName}).");
        process.WaitForExit();

        return default;
    }

    private ProcessStartInfo GetStartInfo()
    {
        var terminal = toolsConfiguration.GetOptions()["terminal"];

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new ProcessStartInfo(terminal);
        }
        else
        {
            return new ProcessStartInfo("open", $". -a {terminal}");
        }
    }
}
