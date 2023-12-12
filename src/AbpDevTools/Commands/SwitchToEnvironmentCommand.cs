using AbpDevTools.Configuration;
using AbpDevTools.Environments;
using CliFx.Infrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        var terminal = toolsConfiguration.GetOptions()["terminal"];

        var startInfo = new ProcessStartInfo(terminal);

        processEnvironmentManager.SetEnvironmentForProcess(EnvironmentName, startInfo);

        var process = Process.Start(startInfo)!;
        console.Output.WriteAsync($"Switched to {EnvironmentName} on the process (PID: {process.Id} - {process.ProcessName}).");
        process.WaitForExit();

        return default;
    }
}
