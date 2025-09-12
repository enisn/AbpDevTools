using AbpDevTools.Configuration;
using AbpDevTools.Environments;
using AbpDevTools.LocalConfigurations;
using AbpDevTools.Notifications;
using AbpDevTools.Services;
using CliFx.Infrastructure;
using Spectre.Console;
using System.Diagnostics;

namespace AbpDevTools.Commands;

[Command("run", Description = "Run all the required applications")]
public partial class RunCommand : ICommand
{
    [CommandParameter(0, IsRequired = false, Description = "Working directory to run build. Probably project or solution directory path goes here. Default: . (Current Directory)")]
    public string? WorkingDirectory { get; set; }

    [CommandOption("watch", 'w', Description = "Watch mode")]
    public bool Watch { get; set; }

    [CommandOption("skip-migrate", Description = "Skips migration and runs projects directly.")]
    public bool SkipMigration { get; set; }

    [CommandOption("all", 'a', Description = "Projects to run will not be asked as prompt. All of them will run.")]
    public bool RunAll { get; set; }

    [CommandOption("no-build", Description = "Skips build before running. Passes '--no-build' parameter to dotnet run.")]
    public bool NoBuild { get; set; }

    [CommandOption("install-libs", 'i', Description = "Runs 'abp install-libs' command while running the project simultaneously.")]
    public bool InstallLibs { get; set; }

    [CommandOption("graphBuild", 'g', Description = "Uses /graphBuild while running the applications. So no need building before running. But it may cause some performance.")]
    public bool GraphBuild { get; set; }

    [CommandOption("projects", 'p', Description = "(Array) Names or part of names of projects will be ran.")]
    public string[] Projects { get; set; } = Array.Empty<string>();

    [CommandOption("configuration", 'c')]
    public string? Configuration { get; set; }

    [CommandOption("env", 'e', Description = "Uses the virtual environment for this process. Use 'abpdev env config' command to see/manage environments.")]
    public string? EnvironmentName { get; set; }

    [CommandOption("retry", 'r', Description = "Retries running again when application exits.")]
    public bool Retry { get; set; }

    [CommandOption("verbose", 'v', Description = "Shows verbose output from the projects.")]
    public bool Verbose { get; set; }

    [CommandOption("yml", Description = "Path to the yml file to be used for running the project.")]
    public string? YmlPath { get; set; }

    protected IConsole? console;

    protected readonly List<RunningProjectItem> runningProjects = new();

    protected readonly INotificationManager notificationManager;
    protected readonly MigrateCommand migrateCommand;
    protected readonly IProcessEnvironmentManager environmentManager;
    protected readonly UpdateCheckCommand updateCheckCommand;
    protected readonly RunnableProjectsProvider runnableProjectsProvider;
    protected readonly ToolsConfiguration toolsConfiguration;
    protected readonly FileExplorer fileExplorer;
    private readonly LocalConfigurationManager localConfigurationManager;
    private readonly IKeyInputManager keyInputManager;

    public RunCommand(
        INotificationManager notificationManager,
        MigrateCommand migrateCommand,
        IProcessEnvironmentManager environmentManager,
        UpdateCheckCommand updateCheckCommand,
        RunnableProjectsProvider runnableProjectsProvider,
        ToolsConfiguration toolsConfiguration,
        FileExplorer fileExplorer,
        LocalConfigurationManager localConfigurationManager,
        IKeyInputManager keyInputManager)
    {
        this.notificationManager = notificationManager;
        this.migrateCommand = migrateCommand;
        this.environmentManager = environmentManager;
        this.updateCheckCommand = updateCheckCommand;
        this.runnableProjectsProvider = runnableProjectsProvider;
        this.toolsConfiguration = toolsConfiguration;
        this.fileExplorer = fileExplorer;
        this.localConfigurationManager = localConfigurationManager;
        this.keyInputManager = keyInputManager;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        this.console = console;
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }

        if (string.IsNullOrEmpty(YmlPath))
        {
            YmlPath = Path.Combine(WorkingDirectory, "abpdev.yml");
        }

        var cancellationToken = console.RegisterCancellationHandler();

        if (localConfigurationManager.TryLoad(YmlPath!, out var localRootConfig, FileSearchDirection.OnlyCurrent))
        {
            console.Output.WriteLine($"Loaded YAML configuration from '{YmlPath}' with environment '{localRootConfig?.Environment?.Name ?? "Default"}'.");
        }

        FileInfo[] csprojs = await AnsiConsole.Status()
            .StartAsync("Looking for projects", async ctx =>
            {
                ctx.Spinner(Spinner.Known.SimpleDotsScrolling);

                await Task.Yield();

                return runnableProjectsProvider.GetRunnableProjects(WorkingDirectory);
            });

        await console.Output.WriteLineAsync($"{csprojs.Length} csproj file(s) found.");

        if (!SkipMigration && localRootConfig?.Run?.SkipMigrate != true)
        {
            migrateCommand.WorkingDirectory = this.WorkingDirectory;
            migrateCommand.NoBuild = this.NoBuild;
            migrateCommand.EnvironmentName = this.EnvironmentName;
            migrateCommand.RunAll = this.RunAll;
            migrateCommand.Projects = this.Projects;

            await migrateCommand.ExecuteAsync(console);
        }

        await console.Output.WriteLineAsync("Starting projects...");

        var projectFiles = csprojs.Where(x => !x.Name.Contains(".DbMigrator")).ToArray();

        if (!RunAll && projectFiles.Length > 1)
        {
            await console.Output.WriteLineAsync($"\n");

            ApplyLocalProjects(localRootConfig);

            if (Projects.Length == 0)
            {
                var choosedProjects = AnsiConsole.Prompt(
                    new MultiSelectionPrompt<string>()
                        .Title("Choose [mediumpurple2]projects[/] to run.")
                        .Required(true)
                        .PageSize(12)
                        .HighlightStyle(new Style(foreground: Color.MediumPurple2))
                        .MoreChoicesText("[grey](Move up and down to reveal more projects)[/]")
                        .InstructionsText(
                            "[grey](Press [mediumpurple2]<space>[/] to toggle a project, " +
                            "[green]<enter>[/] to accept)[/]")
                        .AddChoices(projectFiles.Select(s => s.Name)));

                projectFiles = projectFiles.Where(x => choosedProjects.Contains(x.Name)).ToArray();
            }
            else
            {
                projectFiles = projectFiles.Where(x => Projects.Any(y => x.FullName.Contains(y, StringComparison.InvariantCultureIgnoreCase))).ToArray();
            }
        }

        foreach (var csproj in projectFiles)
        {
            localConfigurationManager.TryLoad(csproj.FullName, out var localConfiguration);

            var commandPrefix = BuildCommandPrefix(localConfiguration?.Run?.Watch);
            var commandSuffix = BuildCommandSuffix(
                localConfiguration?.Run?.NoBuild,
                localConfiguration?.Run?.GraphBuild,
                localConfiguration?.Run?.Configuration);

            var tools = toolsConfiguration.GetOptions();
            var startInfo = new ProcessStartInfo(tools["dotnet"], commandPrefix + $"run --project \"{csproj.FullName}\"" + commandSuffix)
            {
                WorkingDirectory = Path.GetDirectoryName(csproj.FullName),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            localConfigurationManager.ApplyLocalEnvironmentForProcess(csproj.FullName, startInfo, localConfiguration);

            if (!string.IsNullOrEmpty(EnvironmentName))
            {
                environmentManager.SetEnvironmentForProcess(EnvironmentName, startInfo);
            }

            runningProjects.Add(
                new RunningCsProjItem(
                    csproj.Name,
                    Process.Start(startInfo)!,
                    startInfo,
                    verbose: Verbose
                )
            );

            if (InstallLibs)
            {
                var wwwRootLibs = Path.Combine(Path.GetDirectoryName(csproj.FullName)!, "wwwroot/libs");
                if (!Directory.Exists(wwwRootLibs))
                {
                    Directory.CreateDirectory(wwwRootLibs);
                }

                if (!Directory.EnumerateFiles(wwwRootLibs).Any())
                {
                    File.WriteAllText(Path.Combine(wwwRootLibs, "abplibs.installing"), string.Empty);
                }

                var installLibsStartInfo = new ProcessStartInfo(tools["abp"], "install-libs")
                {
                    WorkingDirectory = Path.GetDirectoryName(csproj.FullName),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                };

                var installLibsRunninItem = new RunningInstallLibsItem(
                    csproj.Name.Replace(".csproj", " install-libs"),
                    Process.Start(installLibsStartInfo)!,
                    installLibsStartInfo
                );

                runningProjects.Add(installLibsRunninItem);
            }
        }

        cancellationToken.Register(KillRunningProcesses);

        await RenderProcesses(cancellationToken);

        await updateCheckCommand.SoftCheckAsync(console);
    }

    private void ApplyLocalProjects(LocalConfiguration? localConfiguration)
    {
        if(localConfiguration is not null)
        {
            if (Projects.Length == 0 && localConfiguration?.Run?.Projects.Length > 0)
            {
                Projects = localConfiguration.Run.Projects;
            }
        }
    }

    private string BuildCommandSuffix(bool? noBuild = null, bool? graphBuild = null, string? configuration = null)
    {
        var commandSuffix = (NoBuild || noBuild == true) ? " --no-build" : string.Empty;

        if (GraphBuild || graphBuild == true)
        {
            commandSuffix += " /graphBuild";
        }

        if (configuration != null)
        {
            commandSuffix += $" --configuration {configuration}";
        }
        else if (!string.IsNullOrEmpty(Configuration))
        {
            commandSuffix += $" --configuration {Configuration}";
        }

        return commandSuffix;
    }

    private string BuildCommandPrefix(bool? watchOverride)
    {
        if (watchOverride is not null)
        {
            return watchOverride.Value ? "watch " : string.Empty;
        }
        return Watch ? "watch " : string.Empty;
    }

    private async Task RenderProcesses(CancellationToken cancellationToken)
    {
        var keyCommandHandler = new KeyCommandHandler(runningProjects, console!, cancellationToken);

        // Start key input listening
        keyInputManager.StartListening();

        var restartLive = false;
        KeyPressEventArgs? pendingAction = null;
        var exitRequested = false;

        do
        {
            restartLive = false;
            AnsiConsole.Clear();

            var table = new Table().Ascii2Border();

            await AnsiConsole.Live(table)
              .StartAsync(async ctx =>
              {
                  table.AddColumn("Project").AddColumn("Status");

                  foreach (var project in runningProjects)
                  {
                      table.AddRow(project.Name!, project.Status!);
                  }
                  
                  // Add help section
                  table.AddRow("", "");
                  table.AddRow("[grey]R[/] - Restart | [grey]Ctrl+R[/] - Restart One | [grey]S[/] - Stop | [grey]K[/] - Kill | [grey]H[/] - Help | [grey]Q[/] - Quit", "");
                  
                  ctx.Refresh();

                  while (!cancellationToken.IsCancellationRequested)
                  {
                      // Check for key input
                      var keyEvent = keyInputManager.TryGetNextKey();
                      if (keyEvent != null)
                      {
                          var requiresLiveRestart = keyEvent.Key == ConsoleKey.H || keyEvent.Key == ConsoleKey.R;

                          if (requiresLiveRestart)
                          {
                              // Defer handling until after Live ends to avoid interleaving
                              pendingAction = keyEvent;
                              restartLive = true;
                              break;
                          }

                          var shouldContinue = await keyCommandHandler.HandleKeyPress(keyEvent);
                          if (!shouldContinue)
                          {
                              exitRequested = true;
                              break; // Exit the loop if Q, S, or K was pressed
                          }
                      }

#if DEBUG
                      await Task.Delay(100);
#else
                      await Task.Delay(500);
#endif
                      table.Rows.Clear();

                      foreach (var project in runningProjects)
                      {
                          if (project.IsCompleted)
                          {
                              table.AddRow(project.Name!, $"[green]*[/] {project.Status}");
                          }
                          else
                          {
                              if (project.Process!.HasExited && !project.Queued)
                              {
                                  project.Status = $"[red]*[/] Exited({project.Process.ExitCode})";

                                  if (Retry)
                                  {
                                      project.Status = $"[orange1]*[/] Exited({project.Process.ExitCode})";

                                      _ = RestartProject(project, cancellationToken); // fire and forget
                                  }
                              }
                              table.AddRow(project.Name!, project.Status!);
                          }
                      }
                      
                      // Re-add help section
                      table.AddRow("", "");
                      table.AddRow("[grey]R[/] - Restart | [grey]Ctrl+R[/] - Restart One | [grey]S[/] - Stop | [grey]K[/] - Kill | [grey]H[/] - Help | [grey]Q[/] - Quit", "");

                      ctx.Refresh();
                  }
              });

            if (exitRequested)
            {
                break;
            }

            if (restartLive && pendingAction is not null)
            {
                AnsiConsole.Clear();
                await keyCommandHandler.HandleKeyPress(pendingAction);
                pendingAction = null;
            }

        } while (restartLive && !cancellationToken.IsCancellationRequested);

        // Stop key input listening
        keyInputManager.StopListening();
    }

    private static async Task RestartProject(RunningProjectItem project, CancellationToken cancellationToken = default)
    {
        project.Queued = true;
        await Task.Delay(3100, cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        project.Status = $"[orange1]*[/] Exited({project.Process!.ExitCode}) (Retrying...)";
        project.Process = Process.Start(project.Process!.StartInfo)!;
        project.StartReadingOutput();
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