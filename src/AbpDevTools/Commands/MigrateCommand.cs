using AbpDevTools.Configuration;
using AbpDevTools.Environments;
using AbpDevTools.LocalConfigurations;
using AbpDevTools.Notifications;
using CliFx.Infrastructure;
using Spectre.Console;
using System.Diagnostics;

namespace AbpDevTools.Commands;

[Command("migrate", Description = "Runs all .DbMigrator projects in folder recursively.")]
public class MigrateCommand : ICommand
{
    [CommandParameter(0, IsRequired = false, Description = "Working directory to run build. Probably project or solution directory path goes here. Default: . (Current Directory)")]
    public string? WorkingDirectory { get; set; }

    [CommandOption("no-build", Description = "Skipts build before running. Passes '--no-build' parameter to dotnet run.")]
    public bool NoBuild { get; set; }

    [CommandOption("env", 'e', Description = "Uses the virtual environment for this process. Use 'abpdev env config' command to see/manage environments.")]
    public string? EnvironmentName { get; set; }

    protected readonly List<RunningProjectItem> runningProjects = new();

    protected IConsole? console;

    protected readonly INotificationManager notificationManager;
    protected readonly IProcessEnvironmentManager environmentManager;
    protected readonly ToolsConfiguration toolsConfiguration;
    protected readonly LocalConfigurationManager localConfigurationManager;

    public MigrateCommand(INotificationManager notificationManager, IProcessEnvironmentManager environmentManager, ToolsConfiguration toolsConfiguration, LocalConfigurationManager localConfigurationManager)
    {
        this.notificationManager = notificationManager;
        this.environmentManager = environmentManager;
        this.toolsConfiguration = toolsConfiguration;
        this.localConfigurationManager = localConfigurationManager;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        this.console = console;
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }

        var dbMigrators = Directory.EnumerateFiles(WorkingDirectory, "*.csproj", SearchOption.AllDirectories)
           .Where(x => x.EndsWith("DbMigrator.csproj"))
           .Select(x => new FileInfo(x))
           .ToList();

        var cancellationToken = console.RegisterCancellationHandler();

        if (dbMigrators.Count == 0)
        {
            await console.Output.WriteLineAsync($"No migrator(s) found in this folder. Migration not applied.");
            return;
        }

        await console.Output.WriteLineAsync($"{dbMigrators.Count} db migrator(s) found.");

        var commandPostFix = NoBuild ? " --no-build" : string.Empty;

        foreach (var dbMigrator in dbMigrators)
        {
            var tools = toolsConfiguration.GetOptions();
            var startInfo = new ProcessStartInfo(tools["dotnet"], $"run --project {dbMigrator.FullName}" + commandPostFix)
            {
                WorkingDirectory = Path.GetDirectoryName(dbMigrator.FullName),
                RedirectStandardOutput = true,
            };

            localConfigurationManager.ApplyLocalEnvironmentForProcess(dbMigrator.FullName, startInfo);

            if (!string.IsNullOrEmpty(EnvironmentName))
            {
                environmentManager.SetEnvironmentForProcess(EnvironmentName, startInfo);
            }

            var process = Process.Start(startInfo)!;

            runningProjects.Add(new RunningProjectItem
            {
                Name = dbMigrator.Name,
                Process = process,
                Status = "Running..."
            });
        }

        await console.Output.WriteAsync("Waiting for db migrators to finish...");
        cancellationToken.Register(KillRunningProcesses);

        await RenderStatusAsync();

        if (!cancellationToken.IsCancellationRequested)
        {
            await console.Output.WriteLineAsync("Migrations finished.");
            await notificationManager.SendAsync("Migration Completed", $"Complated migrations in {WorkingDirectory}");
        }

        KillRunningProcesses();
    }

    private async Task RenderStatusAsync()
    {
        var table = new Table().Border(TableBorder.Ascii);

        AnsiConsole.WriteLine(Environment.NewLine);
        await AnsiConsole.Live(table)
            .StartAsync(async ctx =>
            {
                table.AddColumn("Project");
                table.AddColumn("Status");

                UpdateTable(table);
                ctx.UpdateTarget(table);

                foreach (var runningProject in runningProjects)
                {
                    runningProject.Process.OutputDataReceived += (sender, args) =>
                    {
                        if (args?.Data != null && args.Data.Length < 90)
                        {
                            runningProject.Status = args.Data[args.Data.IndexOf(']')..].Replace('[', '\0').Replace(']', '\0');
                            UpdateTable(table);
                            ctx.UpdateTarget(table);
                        }
                    };
                    runningProject.Process.BeginOutputReadLine();
                }

                await Task.WhenAll(runningProjects.Select(x => x.Process.WaitForExitAsync()));
            });
    }

    private void UpdateTable(Table table)
    {
        table.Rows.Clear();
        foreach (var runningProject in runningProjects)
        {
            table.AddRow(
                runningProject.Name,
                runningProject.Status);
        }
    }

    protected void KillRunningProcesses()
    {
        console!.Output.WriteLine($"- Killing running {runningProjects.Count} processes...");
        foreach (var project in runningProjects)
        {
            project.Process.Kill(entireProcessTree: true);

            project.Process.WaitForExit();
        }
    }
}
