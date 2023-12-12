using System.Diagnostics;
using AbpDevTools.Configuration;
using AbpDevTools.Notifications;
using CliFx.Infrastructure;
using Spectre.Console;

namespace AbpDevTools.Commands;

[Command("database-drop", Description = "Drops all databases in the working directory")]
public class DatabaseDropCommand : ICommand
{
    [CommandParameter(0, IsRequired = false, Description = "Working directory to search for EntityFramework projects. Default: . (Current Directory)")]
    public string? WorkingDirectory { get; set; }
    
    [CommandOption("force", 'f')]
    public bool Force { get; set; }
    
    protected readonly INotificationManager notificationManager;
    protected readonly ToolsConfiguration toolsConfiguration;

    public DatabaseDropCommand(INotificationManager notificationManager, ToolsConfiguration toolsConfiguration)
    {
        this.notificationManager = notificationManager;
        this.toolsConfiguration = toolsConfiguration;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }

        var efCoreProjects = await GetEfCoreProjectsAsync();
            
        var cancellationToken = console.RegisterCancellationHandler();

        var projectCount = efCoreProjects.Length;
        if (projectCount == 0)
        {
            await console.Output.WriteLineAsync("Could not find any EntityFrameworkCore project in the working directory...");
            return;
        }
        
        AnsiConsole.MarkupLine($"[green]{projectCount}[/] EntityFrameworkCore project(s) found in the directory. Trying to find and drop databases...");

        var forcePostfix = Force ? " --force" : string.Empty;

        for (var i = 0; i < projectCount; i++)
        {
            var efCoreProject = efCoreProjects[i];

            AnsiConsole.MarkupLine($"[blue]## Project{(i + 1)} - {efCoreProject.Name.Replace(".csproj", string.Empty)}[/]");

            var tools = toolsConfiguration.GetOptions();
            var startInfo = new ProcessStartInfo(tools["dotnet"], $"ef database drop{forcePostfix}")
            {
                WorkingDirectory = efCoreProject.DirectoryName!,
                RedirectStandardOutput = true,
                CreateNoWindow = false
            };

            var process = Process.Start(startInfo)!;
            
            process.OutputDataReceived += async (sender, args) =>
            {
                if (args?.Data != null)
                {
                    await console.Output.WriteLineAsync("* " + args.Data);
                }
            };
            
            process.BeginOutputReadLine();

            await process.WaitForExitAsync(cancellationToken);
        }
        
        if (!cancellationToken.IsCancellationRequested)
        {
            await notificationManager.SendAsync("Dropped database(s)", $"Dropped all databases in {WorkingDirectory}");
        }
    }
    
    private async Task<FileInfo[]> GetEfCoreProjectsAsync()
    {
        return await AnsiConsole.Status()
            .StartAsync("Searching EntityFrameworkCore projects...", ctx =>
            {
                ctx.Spinner(Spinner.Known.SimpleDotsScrolling);
                
                var efCoreProjects = Directory.EnumerateFiles(WorkingDirectory!, "*.csproj", SearchOption.AllDirectories)
                    .Where(x => x.EndsWith("EntityFrameworkCore.csproj"))
                    .Select(x => new FileInfo(x))
                    .ToArray();
                
                return Task.FromResult(efCoreProjects);
            });
    }
}