using System.Diagnostics;
using AbpDevTools.Notifications;
using CliFx.Infrastructure;

namespace AbpDevTools.Commands;

[Command("database-drop", Description = "Drops all databases in the working directory")]
public class DatabaseDropCommand : ICommand
{
    [CommandParameter(0, IsRequired = false, Description = "Working directory to search for EntityFramework projects. Default: . (Current Directory)")]
    public string WorkingDirectory { get; set; }
    
    [CommandOption("force", 'f')]
    public bool Force { get; set; }
    
    protected readonly INotificationManager notificationManager;

    public DatabaseDropCommand(INotificationManager notificationManager)
    {
        this.notificationManager = notificationManager;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }

        var efCoreProjects = Directory.EnumerateFiles(WorkingDirectory, "*.csproj", SearchOption.AllDirectories)
            .Where(x => x.EndsWith("EntityFrameworkCore.csproj"))
            .Select(x => new FileInfo(x))
            .ToList();
            
        var cancellationToken = console.RegisterCancellationHandler();

        var projectCount = efCoreProjects.Count;
        if (projectCount == 0)
        {
            await console.Output.WriteLineAsync("Could not find any EntityFrameworkCore project in the working directory...");
            return;
        }
        
        await console.Output.WriteLineAsync($"{projectCount} EntityFrameworkCore project(s) found in the directory. Trying to find and drop databases...");

        var forcePostfix = Force ? " --force" : string.Empty;

        for (int i = 0; i < projectCount; i++)
        {
            var efCoreProject = efCoreProjects[i];
            
            await console.Output.WriteLineAsync($"## Project {(i + 1)} - {efCoreProject.Name.Replace(".csproj", string.Empty)}");
            
            var startInfo = new ProcessStartInfo("dotnet", $"ef database drop{forcePostfix}")
            {
                WorkingDirectory = efCoreProject.DirectoryName!,
                RedirectStandardOutput = true,
                CreateNoWindow = false
            };

            var process = Process.Start(startInfo)!;
            
            process.OutputDataReceived += (sender, args) =>
            {
                if (args?.Data != null)
                {
                    console.Output.WriteLine("* " + args.Data);
                }
            };
            
            process.BeginOutputReadLine();

            await process.WaitForExitAsync(cancellationToken);
        }
        
        if (!cancellationToken.IsCancellationRequested)
        {
            await notificationManager.SendAsync("Dropped database(s)", $"Dropped database(s) in {WorkingDirectory}");
        }
    }
}