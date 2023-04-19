using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CliFx.Infrastructure;
using Spectre.Console;

namespace AbpDevTools.Commands;

[Command("run", Description = "Run all the required applications")]
public class RunCommand : ICommand
{
    [CommandParameter(0, IsRequired = false, Description = "Working directory to run build. Probably project or solution directory path goes here. Default: . (Current Directory)")]
    public string WorkingDirectory { get; set; }

    [CommandOption("watch", 'w', Description = "Watch mode")]
    public bool Watch { get; set; }

    [CommandOption("skip-migrate", Description = "Skips migration and runs projects directly.")]
    public bool SkipMigration { get; set; }

    [CommandOption("select", 's' , Description = "Projects to run will be asked as prompt. By default runs all of them.")]
    public bool SelectProjectToRun { get; set; }

    [CommandOption("no-build", Description = "Skipts build before running. Passes '--no-build' parameter to dotnet run.")]
    public bool NoBuild { get; set; }

    protected IConsole console;

    protected readonly List<RunningProjectItem> runningProjects = new();

    private static readonly string[] _runnableProjects = new string[]{
        ".HttpApi.Host",
        ".Web",
        ".AuthServer",
        ".Web.Host",
        ".Blazor.Host",
        ".Blazor",
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
            .Where(x => _runnableProjects.Any(y => x.EndsWith(y + ".csproj")))
            .Select(x => new FileInfo(x))
            .ToList();

        await console.Output.WriteLineAsync($"{csprojs.Count} csproj file(s) found.");

        if (!SkipMigration)
        {
            await new MigrateCommand()
            {
                WorkingDirectory = this.WorkingDirectory
            }.ExecuteAsync(console);
        }

        await console.Output.WriteLineAsync("Starting projects...");

        var projects = csprojs.Where(x => !x.Name.Contains(".DbMigrator")).ToArray();

        if (SelectProjectToRun)
        {
            await console.Output.WriteLineAsync($"\n");
            var choosedProjects = AnsiConsole.Prompt(
                new MultiSelectionPrompt<string>()
                    .Title("Choose [green]projects[/] to run.")
                    .Required(true)
                    .PageSize(12)
                    .MoreChoicesText("[grey](Move up and down to reveal more projects)[/]")
                    .InstructionsText(
                        "[grey](Press [blue]<space>[/] to toggle a project, " +
                        "[green]<enter>[/] to accept)[/]")
                    .AddChoices(projects.Select(s => s.Name)));

            projects = projects.Where(x => choosedProjects.Contains(x.Name)).ToArray();
        }

        var watchCommand = Watch ? "watch " : string.Empty;
        var noBuildCommand = NoBuild ? "--no-build" : string.Empty;

        foreach (var csproj in projects)
        {
            runningProjects.Add(new RunningProjectItem
            {
                Name = csproj.Name,
                Process = Process.Start(new ProcessStartInfo("dotnet", watchCommand + $"run --project {csproj.FullName}" + noBuildCommand)
                {
                    WorkingDirectory = Path.GetDirectoryName(csproj.FullName),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                }),
                Status = "Building..."
            });
        }

        Task.Factory.StartNew(() => RenderProcesses(cancellationToken));

        await Task.WhenAll(runningProjects.Select(x => x.Process.WaitForExitAsync(cancellationToken)));
    }

    private async void RenderProcesses(CancellationToken cancellationToken)
    {
        foreach (var project in runningProjects)
        {
            project.Process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null && args.Data.Contains("Now listening on: "))
                {
                    project.Status = args.Data[args.Data.IndexOf("Now listening on: ")..];
                    project.Process.CancelOutputRead();
                    project.IsRunning = true;
                }

                if (DateTime.Now - project.Process.StartTime > TimeSpan.FromMinutes(2))
                {
                    project.Process.CancelOutputRead();
                }
            };
            project.Process.BeginOutputReadLine();
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1000);
            console.Clear();
            foreach (var project in runningProjects)
            {
                if (project.IsRunning)
                {
                    using (console.WithForegroundColor(ConsoleColor.Green))
                    {
                        await console.Output.WriteLineAsync($"{project.Name} - Running - {project.Status}");
                    }
                }
                else
                {
                    if (project.Process.HasExited)
                    {
                        project.Status = $"Exited({project.Process.ExitCode})";
                    }
                    await console.Output.WriteLineAsync($"{project.Name} - {project.Status}");
                }
            }
        }
    }
}