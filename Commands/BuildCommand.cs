using CliFx.Infrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AbpDevTools.Commands;

[Command("build", Description = "Shortcut for dotnet build /graphBuild")]
public class BuildCommand : ICommand
{
    [CommandParameter(0, IsRequired = false, Description = "Working directory to run build. Probably project or solution directory path goes here. Default: . (Current Directory)")]
    public string WorkingDirectory { get; set; }

    Process runningProcess;

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }

        var cancellationToken = console.RegisterCancellationHandler();

        runningProcess = Process.Start(new ProcessStartInfo("dotnet", "build /graphBuild")
        {
            WorkingDirectory = WorkingDirectory
        });

        await runningProcess.WaitForExitAsync(cancellationToken);
    }
}
