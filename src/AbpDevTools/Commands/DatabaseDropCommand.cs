using System.Diagnostics;
using AbpDevTools.Configuration;
using AbpDevTools.Notifications;
using AbpDevTools.Services;
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
    
    [CommandOption("package", 'p', Description = "Filter projects by direct package reference. Only projects with this package will have their databases dropped.")]
    public string? PackageFilter { get; set; }
    
    protected readonly INotificationManager notificationManager;
    protected readonly ToolsConfiguration toolsConfiguration;
    protected readonly EntityFrameworkCoreProjectsProvider efCoreProjectsProvider;
    protected readonly DotnetDependencyResolver dependencyResolver;

    public DatabaseDropCommand(
        INotificationManager notificationManager, 
        ToolsConfiguration toolsConfiguration,
        EntityFrameworkCoreProjectsProvider efCoreProjectsProvider,
        DotnetDependencyResolver dependencyResolver)
    {
        this.notificationManager = notificationManager;
        this.toolsConfiguration = toolsConfiguration;
        this.efCoreProjectsProvider = efCoreProjectsProvider;
        this.dependencyResolver = dependencyResolver;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }

        var cancellationToken = console.RegisterCancellationHandler();
        var efCoreProjects = await GetEfCoreProjectsAsync(cancellationToken);

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
    
    private async Task<FileInfo[]> GetEfCoreProjectsAsync(CancellationToken cancellationToken)
    {
        return await AnsiConsole.Status()
            .StartAsync("Searching EntityFrameworkCore projects with design-time tools...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.SimpleDotsScrolling);
                
                var efCoreProjects = await efCoreProjectsProvider.GetEfCoreToolsProjectsAsync(WorkingDirectory!, cancellationToken);
                
                // Filter by package reference if specified
                if (!string.IsNullOrEmpty(PackageFilter))
                {
                    ctx.Status($"Filtering projects with direct reference to '{PackageFilter}'...");
                    var filteredProjects = new List<FileInfo>();
                    
                    foreach (var project in efCoreProjects)
                    {
                        if (await dependencyResolver.HasDirectPackageReferenceAsync(project.FullName, PackageFilter, cancellationToken))
                        {
                            filteredProjects.Add(project);
                        }
                    }
                    
                    return filteredProjects.ToArray();
                }
                
                return efCoreProjects;
            });
    }
}