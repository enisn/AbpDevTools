using AbpDevTools.Services;
using CliFx.Infrastructure;
using Spectre.Console;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AbpDevTools.Commands;

[Command("add-migration", Description = "Adds migration with specified name in EntityFrameworkCore project(s). Used to add bulk migrations to multiple project at the same time.")]
public class AddMigrationCommand : ICommand
{
    [CommandParameter(0, IsRequired = false, Description = "Working directory to run build. Probably project or solution directory path goes here. Default: . (Current Directory)")]
    public string? WorkingDirectory { get; set; }

    [CommandOption("name", 'n', Description = "Name of the migration.")]
    public string Name { get; set; } = "Initial";

    [CommandOption("all", 'a', Description = "Add migration to all EF Core projects.")]
    public bool All { get; set; }

    public List<RunningProgressItem> RunningProgresses { get; } = new();

    protected readonly EntityFrameworkCoreProjectsProvider entityFrameworkCoreProjectsProvider;

    public AddMigrationCommand(EntityFrameworkCoreProjectsProvider entityFrameworkCoreProjectsProvider)
    {
        this.entityFrameworkCoreProjectsProvider = entityFrameworkCoreProjectsProvider;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }

        var cancellationToken = console.RegisterCancellationHandler();

        var projectFiles = GetEfCoreProjects();

        if (projectFiles.Length == 0)
        {
            await console.Output.WriteLineAsync("No EF Core projects found. No migrations to add.");
            return;
        }

        if (!All)
        {
            var chosenProjects = AnsiConsole.Prompt(new MultiSelectionPrompt<string>()
                .Title("Choose project to create migrations.")
                .Required(true)
                .PageSize(12)
                .HighlightStyle(new Style(foreground: Color.MediumPurple2))
                .MoreChoicesText("[grey](Move up and down to reveal more projects)[/]")
                .InstructionsText(
                            "[grey](Press [mediumpurple2]<space>[/] to toggle a project, " +
                            "[green]<enter>[/] to accept)[/]")
                .AddChoices(projectFiles.Select(p => p.FullName).ToArray())
            );

            projectFiles = projectFiles.Where(p => chosenProjects.Contains(p.FullName)).ToArray();
        }

        foreach (var project in projectFiles)
        {
            var arguments = $"ef migrations add {Name} --project {project.FullName}";
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
                WorkingDirectory = WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            var projectName = Path.GetFileNameWithoutExtension(project.Name);
            RunningProgresses.Add(new RunningProgressItem(process, projectName, "Running..."));

            process.Start();
        }

        cancellationToken.Register(KillAllProcesses);
        await RenderProgressesAsync(cancellationToken);
    }

    FileInfo[] GetEfCoreProjects()
    {
        return entityFrameworkCoreProjectsProvider.GetEfCoreProjects(WorkingDirectory!);
    }

    private async Task RenderProgressesAsync(CancellationToken cancellationToken)
    {
        var table = new Table()
            .AddColumn("Project")
            .AddColumn("Status")
            .AddColumn("Result");

        await AnsiConsole.Live(table).StartAsync(async ctx =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                RenderProgresses(table);
                await Task.Delay(500, cancellationToken);

                ctx.Refresh();
            }
        });
    }

    private async void RenderProgresses(Table table)
    {
        table.Rows.Clear();
        foreach (var progress in RunningProgresses)
        {
            if (progress.Process.HasExited)
            {
                progress.Status = progress.ExitCode == 0 ? "Completed!"
                    : $"Failed! ( Exit Code: {progress.ExitCode})";
            }

            table.AddRow(progress.Name, progress.Status, progress.LastLine);
        }
    }

    protected void KillAllProcesses()
    {
        foreach (var progress in RunningProgresses)
        {
            if (progress.IsRunning)
            {
                progress.Process.Kill(entireProcessTree: true);
            }
        }
    }
}

public class RunningProgressItem
{
    public RunningProgressItem(Process process, string name, string initialStatus)
    {
        Process = process;
        Name = name;
        Status = initialStatus;
    }

    public string Name { get; set; }

    public Process Process { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Output { get; set; }

    public string LastLine { get; set; } = string.Empty;

    public int ExitCode => Process.HasExited ? Process.ExitCode : 0;

    public bool IsRunning => Process.HasExited == false;
}
