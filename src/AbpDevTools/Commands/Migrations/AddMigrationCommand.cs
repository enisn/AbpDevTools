using AbpDevTools.Services;
using CliFx.Infrastructure;
using Spectre.Console;
using System.Diagnostics;

namespace AbpDevTools.Commands.Migrations;

[Command("migrations add", Description = "Adds migration with specified name in EntityFrameworkCore project(s). Used to add bulk migrations to multiple project at the same time.")]
public class AddMigrationCommand : MigrationsCommandBase, ICommand
{
    [CommandOption("name", 'n', Description = "Name of the migration.")]
    public string Name { get; set; } = "Initial";

    public List<RunningProgressItem> RunningProgresses { get; } = new();


    public AddMigrationCommand(EntityFrameworkCoreProjectsProvider entityFrameworkCoreProjectsProvider) : base(entityFrameworkCoreProjectsProvider)
    {
    }

    public override async ValueTask ExecuteAsync(IConsole console)
    {
        await base.ExecuteAsync(console);

        var cancellationToken = console.RegisterCancellationHandler();

        var projectFiles = await ChooseProjectsAsync();

        if (projectFiles.Length == 0)
        {
            await console.Output.WriteLineAsync("No EF Core projects found. No migrations to add.");
            return;
        }

        foreach (var project in projectFiles)
        {
            var arguments = $"migrations add {Name} --project {project.FullName}";
            var process = new Process
            {
                StartInfo = new ProcessStartInfo("dotnet-ef", arguments)
                {
                    WorkingDirectory = WorkingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };

            var projectName = Path.GetFileNameWithoutExtension(project.Name);
            RunningProgresses.Add(new RunningProgressItem(process!, projectName, "Running..."));
        }

        cancellationToken.Register(KillAllProcesses);
        await RenderProgressesAsync(cancellationToken);
    }

    private async Task RenderProgressesAsync(CancellationToken cancellationToken)
    {
        var table = new Table().Border(TableBorder.Rounded)
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

                if (RunningProgresses.All(p => !p.IsRunning))
                {
                    break;
                }
            }

            await Task.WhenAll(RunningProgresses.Select(p => p.Process.WaitForExitAsync()));

            RenderProgresses(table);
            ctx.Refresh();
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
        process.OutputDataReceived += OutputReceived;
        process.Start();
        process.BeginOutputReadLine();
    }

    private void OutputReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data != null)
        {
            Output += e.Data + Environment.NewLine;
            LastLine = e.Data;
        }
    }

    public string Name { get; set; }

    public Process Process { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Output { get; set; } = string.Empty;

    public string LastLine { get; set; } = string.Empty;

    public int ExitCode => Process.HasExited ? Process.ExitCode : 0;

    public bool IsRunning => Process.HasExited == false;
}
