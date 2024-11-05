using AbpDevTools.Configuration;
using AbpDevTools.Environments;
using AbpDevTools.LocalConfigurations;
using AbpDevTools.Notifications;
using AbpDevTools.Services;
using CliFx.Infrastructure;
using Spectre.Console;
using System.Diagnostics;
using System.Text;

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
    protected readonly RunnableProjectsProvider runnableProjectsProvider;

    public MigrateCommand(INotificationManager notificationManager,
    IProcessEnvironmentManager environmentManager,
    ToolsConfiguration toolsConfiguration,
    LocalConfigurationManager localConfigurationManager,
    RunnableProjectsProvider runnableProjectsProvider)
    {
        this.notificationManager = notificationManager;
        this.environmentManager = environmentManager;
        this.toolsConfiguration = toolsConfiguration;
        this.localConfigurationManager = localConfigurationManager;
        this.runnableProjectsProvider = runnableProjectsProvider;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        this.console = console;
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }

        var dbMigrators = Directory.EnumerateFiles(WorkingDirectory, "*.csproj", SearchOption.AllDirectories)
           .Where(IsDbMigrator)
           .Select(x => new FileInfo(x))
           .ToList();

        var cancellationToken = console.RegisterCancellationHandler();

        if (dbMigrators.Count == 0)
        {
            await console.Output.WriteLineAsync($"No migrator(s) found in this folder. Migration not applied.");
            await RunParameterMigrationFallbackAsync();
            return;
        }

        await console.Output.WriteLineAsync($"{dbMigrators.Count} db migrator(s) found.");

        var commandPostFix = NoBuild ? " --no-build" : string.Empty;

        foreach (var dbMigrator in dbMigrators)
        {
            var tools = toolsConfiguration.GetOptions();
            var startInfo = new ProcessStartInfo(tools["dotnet"], $"run --project \"{dbMigrator.FullName}\"" + commandPostFix)
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

    protected async Task RunParameterMigrationFallbackAsync()
    {
        if (!AnsiConsole.Confirm("Do you want to run any of projects in this folder with '--migrate-database' parameter?"))
        {
            return;
        }

        FileInfo[] csprojs = await AnsiConsole.Status()
            .StartAsync("Looking for projects", async ctx =>
            {
                ctx.Spinner(Spinner.Known.SimpleDotsScrolling);

                await Task.Yield();

                return runnableProjectsProvider.GetRunnableProjects(WorkingDirectory);
            });

        if (csprojs.Length <= 0)
        {
            await console.Output.WriteLineAsync("No project found to run.");
            return;
        }

        if(csprojs.Length == 1)
        {
            await console.Output.WriteLineAsync("Only one project found. Running it with '--migrate-database' parameter.");
            await RunProjectWithMigrateDatabaseAsync(csprojs[0]);
        }
        else{

            var selectedProject = AnsiConsole.Prompt(
                new SelectionPrompt<FileInfo>()
                    .Title("Select a project to run with '--migrate-database' parameter")
                    .PageSize(10)
                    .AddChoices(csprojs)
            );

            await RunProjectWithMigrateDatabaseAsync(selectedProject);
        }

        await RenderStatusAsync();
    }

    protected Task RunProjectWithMigrateDatabaseAsync(FileInfo project)
    {
        var tools = toolsConfiguration.GetOptions();
        var startInfo = new ProcessStartInfo(tools["dotnet"], $"run --project \"{project.FullName}\" -- --migrate-database")
        {
            WorkingDirectory = Path.GetDirectoryName(project.FullName),
            RedirectStandardOutput = true,
        };

        localConfigurationManager.ApplyLocalEnvironmentForProcess(project.FullName, startInfo);

        if (!string.IsNullOrEmpty(EnvironmentName))
        {
            environmentManager.SetEnvironmentForProcess(EnvironmentName, startInfo);
        }

        var process = Process.Start(startInfo)!;

        runningProjects.Add(new RunningProjectItem
        {
            Name = project.Name,
            Process = process,
            Status = "Running..."
        });

        return Task.CompletedTask;
    }

    private bool IsDbMigrator(string file)
    {
        if (!file.EndsWith("Migrator.csproj", StringComparison.InvariantCultureIgnoreCase))
        {
            return false;
        }

        using var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read);
        using var streamReader = new StreamReader(fileStream, Encoding.UTF8, true);

        while (!streamReader.EndOfStream)
        {
            var line = streamReader.ReadLine();
            
            if (line == null)
            {
                continue;
            }

            if (line.Contains("<OutputType>Exe</OutputType>"))
            {
                return true;
            }

            if (line.Contains("</PropertyGroup>"))
            {
                break;
            }
        }

        return false;
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
                    runningProject.Process!.OutputDataReceived += (sender, args) =>
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

                await Task.WhenAll(runningProjects.Select(x => x.Process!.WaitForExitAsync()));
            });
    }

    private void UpdateTable(Table table)
    {
        table.Rows.Clear();
        foreach (var runningProject in runningProjects)
        {
            table.AddRow(
                runningProject.Name!,
                runningProject.Status!);
        }
    }

    protected void KillRunningProcesses()
    {
        console!.Output.WriteLine($"- Killing running {runningProjects.Count} processes...");
        foreach (var project in runningProjects)
        {
            project.Process?.Kill(entireProcessTree: true);

            project.Process?.WaitForExit();
        }
    }
}
