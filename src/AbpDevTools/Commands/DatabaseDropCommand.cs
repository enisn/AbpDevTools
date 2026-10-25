using System.Diagnostics;
using CliFx.Exceptions;
using AbpDevTools.Configuration;
using AbpDevTools.Environments;
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
    
    [CommandOption("env", 'e', Description = "Uses the virtual environment for this process. Use 'abpdev env config' command to see/manage environments.")]
    public string? EnvironmentName { get; set; }
    
    protected readonly INotificationManager notificationManager;
    protected readonly ToolsConfiguration toolsConfiguration;
    protected readonly EntityFrameworkCoreProjectsProvider efCoreProjectsProvider;
    protected readonly DotnetDependencyResolver dependencyResolver;
    protected readonly IProcessEnvironmentManager environmentManager;
    protected readonly DesignTimeEnvironmentGuard environmentGuard;

    public DatabaseDropCommand(
        INotificationManager notificationManager, 
        ToolsConfiguration toolsConfiguration,
        EntityFrameworkCoreProjectsProvider efCoreProjectsProvider,
        DotnetDependencyResolver dependencyResolver,
        IProcessEnvironmentManager environmentManager,
        DesignTimeEnvironmentGuard environmentGuard)
    {
        this.notificationManager = notificationManager;
        this.toolsConfiguration = toolsConfiguration;
        this.efCoreProjectsProvider = efCoreProjectsProvider;
        this.dependencyResolver = dependencyResolver;
        this.environmentManager = environmentManager;
        this.environmentGuard = environmentGuard;
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

        // Prepare every process and preflight every selected project before the first drop.
        var starts = efCoreProjects.Select(project => new ProcessStartInfo(
            toolsConfiguration.GetOptions()["dotnet"], $"ef database drop{forcePostfix}")
        {
            WorkingDirectory = project.DirectoryName!,
            RedirectStandardOutput = true,
            CreateNoWindow = false
        }).ToArray();
        if (!string.IsNullOrEmpty(EnvironmentName))
        {
            foreach (var start in starts)
                environmentManager.SetEnvironmentForProcess(EnvironmentName, start);
            var checks = efCoreProjects.Select(environmentGuard.Inspect).ToArray();
            var missing = checks.Where(c => c.Support == EnvironmentSupport.Missing).ToArray();
            if (missing.Length > 0)
                throw new CommandException("Database drop blocked: design-time configuration is missing AddEnvironmentVariables().\n" +
                    string.Join("\n", missing.Select(c => c.Context)) +
                    "\nAdd AddEnvironmentVariables() before Build() in the design-time factory configuration. Environment variables such as ConnectionStrings__Default otherwise cannot override JSON configuration. No databases were dropped.");
            foreach (var check in checks.Where(c => c.Support == EnvironmentSupport.Unresolved))
                await console.Error.WriteLineAsync($"\n!!! WARNING: DATABASE DROP ENVIRONMENT NOT VERIFIED !!!\n{check.Context}\nAbpDevTools could not verify AddEnvironmentVariables(). The selected virtual environment may not affect the effective EF Core connection string. Check the design-time factory and add AddEnvironmentVariables() before Build() if needed. Source inspection does not prove runtime configuration correctness.\n");
        }

        for (var i = 0; i < projectCount; i++)
        {
            var efCoreProject = efCoreProjects[i];

            AnsiConsole.MarkupLine($"[blue]## Project{(i + 1)} - {efCoreProject.Name.Replace(".csproj", string.Empty)}[/]");

            await DropDatabaseAsync(starts[i], console, cancellationToken);
        }
        
        if (!cancellationToken.IsCancellationRequested)
        {
            await notificationManager.SendAsync("Dropped database(s)", $"Dropped all databases in {WorkingDirectory}");
        }
    }

    protected virtual async Task DropDatabaseAsync(ProcessStartInfo startInfo, IConsole console, CancellationToken cancellationToken)
    {
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
