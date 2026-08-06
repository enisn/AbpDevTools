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

    [CommandOption("all", 'a', Description = "Projects to run will not be asked as prompt. All of them will run.")]
    public bool RunAll { get; set; }

    [CommandOption("projects", 'p', Description = "(Array) Names or part of names of projects will be ran.")]
    public string[] Projects { get; set; } = Array.Empty<string>();

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

    public ValueTask ExecuteAsync(IConsole console)
    {
        return ExecuteAsync(console, localRootConfiguration: null, rootConfigurationLoaded: false);
    }

    internal async ValueTask ExecuteAsync(
        IConsole console,
        LocalConfiguration? localRootConfiguration,
        bool rootConfigurationLoaded)
    {
        this.console = console;
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }

        LoadLocalConfiguration(console, localRootConfiguration, rootConfigurationLoaded);

        var dbMigrators = FindDbMigrators(out var discoveredDbMigratorCount);

        var cancellationToken = console.RegisterCancellationHandler();

        if (dbMigrators.Length == 0)
        {
            var message = discoveredDbMigratorCount > 0
                ? "No db migrator matched the specified project filters."
                : "No migrator(s) found in this folder. Migration not applied.";
            await console.Output.WriteLineAsync(message);
            await RunParameterMigrationFallbackAsync();
            return;
        }

        await console.Output.WriteLineAsync($"{dbMigrators.Length} db migrator(s) found.");

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

    protected void LoadLocalConfiguration(
        IConsole console,
        LocalConfiguration? localRootConfiguration = null,
        bool rootConfigurationLoaded = false)
    {
        if (!rootConfigurationLoaded &&
            TryLoadRootConfiguration(out localRootConfiguration, out var loadedYmlPath))
        {
            console.Output.WriteLine($"Loaded YAML configuration from '{loadedYmlPath}' with environment '{localRootConfiguration?.Environment?.Name ?? "Default"}'.");
        }

        ApplyLocalProjects(localRootConfiguration);
    }

    protected bool TryLoadRootConfiguration(
        out LocalConfiguration? localConfiguration,
        out string? loadedPath)
    {
        return localConfigurationManager.TryLoad(
            Path.Combine(WorkingDirectory!, "abpdev.yml"),
            out localConfiguration,
            out loadedPath);
    }

    private void ApplyLocalProjects(LocalConfiguration? localConfiguration)
    {
        if (Projects.Length == 0 && localConfiguration?.Run?.Projects.Length > 0)
        {
            Projects = localConfiguration.Run.Projects;
        }
    }

    protected FileInfo[] FindDbMigrators(out int discoveredCount)
    {
        var dbMigrators = Directory
            .EnumerateFiles(WorkingDirectory!, "*.csproj", SearchOption.AllDirectories)
            .Where(IsDbMigrator)
            .Select(path => new FileInfo(path))
            .ToArray();

        discoveredCount = dbMigrators.Length;
        return FilterProjects(dbMigrators);
    }

    private FileInfo[] FilterProjects(IEnumerable<FileInfo> projectFiles)
    {
        var projects = projectFiles.ToArray();

        if (RunAll || Projects.Length == 0)
        {
            return projects;
        }

        return projects
            .Where(project => Projects.Any(filter =>
                project.FullName.Contains(filter, StringComparison.InvariantCultureIgnoreCase)))
            .ToArray();
    }

    protected async Task RunParameterMigrationFallbackAsync()
    {
        var canUseInteractiveConsole = global::AbpDevTools.ConsoleSupport.SupportsInteractiveConsole(console);

        FileInfo[] csprojs = await AnsiConsole.Status()
            .StartAsync("Looking for projects that support '--migrate-database' parameter...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.SimpleDotsScrolling);

                await Task.Yield();

                return runnableProjectsProvider.GetRunnableProjectsWithMigrateDatabaseParameter(WorkingDirectory!);
            });

        if (csprojs.Length <= 0)
        {
            await console!.Output.WriteLineAsync("No project found to migrate database.");
            return;
        }

        if (!RunAll && Projects.Length == 0 && !global::AbpDevTools.ConsoleSupport.ConfirmOrDefault(
            console,
            "Do you want to run any of projects in this folder with '--migrate-database' parameter?",
            defaultValue: false,
            fallbackMessage: "Interactive migration confirmation is unavailable; skipping '--migrate-database' fallback projects. Pass '--all' or '--projects' to run them non-interactively."))
        {
            return;
        }

        var projectFiles = FilterProjects(csprojs);

        if (projectFiles.Length == 0)
        {
            await console!.Output.WriteLineAsync("No project matched the specified project filters.");
            return;
        }

        if (!RunAll && Projects.Length == 0 && projectFiles.Length > 1)
        {
            if (canUseInteractiveConsole)
            {
                var selectedProjects = AnsiConsole.Prompt(
                    new MultiSelectionPrompt<FileInfo>()
                        .Title("Select project(s) to run with '--migrate-database' parameter")
                        .Required(true)
                        .PageSize(10)
                        .MoreChoicesText("[grey](Move up and down to reveal more projects)[/]")
                        .InstructionsText("[grey](Press [blue]<space>[/] to toggle a project, [green]<enter>[/] to accept)[/]")
                        .UseConverter(file => Path.GetRelativePath(WorkingDirectory!, file.FullName))
                        .AddChoices(projectFiles)
                );

                projectFiles = selectedProjects.ToArray();
            }
            else
            {
                await console!.Output.WriteLineAsync("Interactive migration project selection is unavailable; running all matching '--migrate-database' projects.");
            }
        }

        foreach (var selectedProject in projectFiles)
        {
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
        if (!global::AbpDevTools.ConsoleSupport.SupportsInteractiveConsole(console))
        {
            await RenderStatusWithoutInteractiveConsoleAsync();
            return;
        }

        var table = new Table().Border(TableBorder.Rounded);

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
                    AttachMigrationOutputReader(runningProject, _ =>
                    {
                        UpdateTable(table);
                        ctx.UpdateTarget(table);
                    });
                }

                await Task.WhenAll(runningProjects.Select(x => x.Process!.WaitForExitAsync()));
            });
    }

    private async Task RenderStatusWithoutInteractiveConsoleAsync()
    {
        var lastStatuses = new Dictionary<RunningProjectItem, string?>();
        var statusLock = new object();

        await console!.Output.WriteLineAsync("Interactive console features are unavailable; streaming migration status updates without the live dashboard.");

        foreach (var runningProject in runningProjects)
        {
            lastStatuses[runningProject] = runningProject.Status;
            await console.Output.WriteLineAsync($"- {runningProject.Name}: {runningProject.Status}");

            AttachMigrationOutputReader(runningProject, project =>
            {
                lock (statusLock)
                {
                    if (lastStatuses.TryGetValue(project, out var lastStatus) && string.Equals(lastStatus, project.Status, StringComparison.Ordinal))
                    {
                        return;
                    }

                    lastStatuses[project] = project.Status;
                    console.Output.WriteLine($"- {project.Name}: {project.Status}");
                }
            });
        }

        await Task.WhenAll(runningProjects.Select(x => x.Process!.WaitForExitAsync()));
    }

    private void AttachMigrationOutputReader(RunningProjectItem runningProject, Action<RunningProjectItem> onStatusChanged)
    {
        runningProject.Process!.OutputDataReceived += (sender, args) =>
        {
            if (args?.Data == null || args.Data.Length >= 90)
            {
                return;
            }

            var indexOfBracket = args.Data.IndexOf(']');
            if (indexOfBracket >= 0 && indexOfBracket < args.Data.Length)
            {
                runningProject.Status = args.Data[indexOfBracket..].Replace('[', '\0').Replace(']', '\0');
            }
            else
            {
                runningProject.Status = args.Data;
            }

            onStatusChanged(runningProject);
        };

        runningProject.Process.BeginOutputReadLine();
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
