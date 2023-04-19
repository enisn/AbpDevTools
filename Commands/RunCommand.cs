using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CliFx.Infrastructure;

namespace AbpDevTools.Commands;

[Command("run", Description = "Run all the required applications")]
public class RunCommand : ICommand
{
    [CommandParameter(0, IsRequired = false, Description = "Working directory to run build. Probably project or solution directory path goes here. Default: . (Current Directory)")]
    public string WorkingDirectory { get; set; }

    [CommandOption("watch", 'w', Description = "Watch mode")]
    public bool Watch { get; set; }

    protected IConsole console;

    protected readonly List<RunningProjectItem> runningProjects = new();

    private static readonly string[] _runnableProjects = new string[]{
        ".HttpApi.Host",
        ".Web",
        ".AuthServer",
        ".Web.Host",
        ".Blazor.Host",
        ".Blazor",
        ".DbMigrator"
    };

    public async ValueTask ExecuteAsync(IConsole console)
    {
        this.console = console;
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }
        var cancellationToken = console.RegisterCancellationHandler();

        var csprojs = Directory.EnumerateFiles(WorkingDirectory, "*.csproj", SearchOption.AllDirectories)
            .Where(x => _runnableProjects.Any(y => x.Contains(y)))
            .Select(x => new FileInfo(x))
            .ToList();

        await console.Output.WriteLineAsync($"{csprojs.Count} csproj file(s) found.");

        var dbMigrators = csprojs.Where(x => x.Name.Contains(".DbMigrator")).ToList();

        await console.Output.WriteLineAsync($"{dbMigrators.Count} db migrator(s) found and prioritized.");

        foreach (var dbMigrator in dbMigrators)
        {
            runningProjects.Add( new RunningProjectItem
            { 
                Name = dbMigrator.Name, 
                Process =Process.Start(new ProcessStartInfo("dotnet", $"run --project {dbMigrator.FullName}")
                {
                    WorkingDirectory = WorkingDirectory
                }),
                Status = "Building..."
            });
        }

        await console.Output.WriteAsync("Waiting for db migrators to finish...");
        await Task.WhenAll(runningProjects.Select(x => x.Process.WaitForExitAsync(cancellationToken)));

        await console.Output.WriteLineAsync("Migrations finished.");
        runningProjects.Clear();

        await console.Output.WriteLineAsync("Starting other projects...");
        foreach (var csproj in csprojs.Where(x => !x.Name.Contains(".DbMigrator")))
        {
            runningProjects.Add(new RunningProjectItem{
                Name = csproj.Name, 
                Process = Process.Start(new ProcessStartInfo("dotnet", $"run --project {csproj.FullName}")
                {
                    WorkingDirectory = WorkingDirectory,
                    UseShellExecute = true
                }),
                Status = "Building..."
            });
        }

        Task.Factory.StartNew(()=>RenderProcesses(cancellationToken));
        await Task.WhenAll(runningProjects.Select(x => x.Process.WaitForExitAsync(cancellationToken)));
    }

    private async void RenderProcesses(CancellationToken cancellationToken)
    {
        foreach (var project in runningProjects)
        {
            project.Process.OutputDataReceived += (sender, args) => 
            {
                if(args.Data != null && args.Data.Contains("Now listening on: "))
                {
                    project.Status = args.Data.Substring(args.Data.IndexOf("Now listening on: "));
                }
            };
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1000);
            Console.Clear();
            foreach (var project in runningProjects)
            {
                Console.WriteLine($"{project.Name} - {project.Process.Id} - {project.Status}");
            }
        }
    }

    public class RunningProjectItem
    {
        public string Name { get; set; }
        public Process Process { get; set; }
        public string Status { get; set; }
    }
}