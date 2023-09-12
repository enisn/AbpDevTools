using AbpDevTools.Configuration;
using AbpDevTools.Environments;
using AbpDevTools.Notifications;
using CliFx.Infrastructure;
using Spectre.Console;
using System.Diagnostics;

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

    [CommandOption("all", 'a', Description = "Projects to run will not be asked as prompt. All of them will run.")]
    public bool RunAll { get; set; }

    [CommandOption("no-build", Description = "Skips build before running. Passes '--no-build' parameter to dotnet run.")]
    public bool NoBuild { get; set; }

    [CommandOption("install-libs", 'i', Description = "Runs 'abp install-libs' command while running the project simultaneously.")]
    public bool InstallLibs { get; set; }

    [CommandOption("graphBuild", 'g', Description = "Uses /graphBuild while running the applications. So no need building before running. But it may cause some performance.")]
    public bool GraphBuild { get; set; }

    [CommandOption("projects", 'p', Description = "(Array) Names or part of names of projects will be ran.")]
    public string[] Projects { get; set; }

    [CommandOption("configuration", 'c')]
    public string Configuration { get; set; }

    [CommandOption("env", 'e', Description = "Uses the virtual environment for this process. Use 'abpdev env config' command to see/manage environments.")]
    public string EnvironmentName { get; set; }

    [CommandOption("retry", 'r', Description = "Retries running again when application exits.")]
    public bool Retry { get; set; }

    protected IConsole console;

    protected readonly List<RunningProjectItem> runningProjects = new();

    protected readonly INotificationManager notificationManager;
    protected readonly MigrateCommand migrateCommand;
    protected readonly IProcessEnvironmentManager environmentManager;

    public RunCommand(INotificationManager notificationManager, MigrateCommand migrateCommand, IProcessEnvironmentManager environmentManager)
    {
        this.notificationManager = notificationManager;
        this.migrateCommand = migrateCommand;
        this.environmentManager = environmentManager;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        this.console = console;
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }
        var cancellationToken = console.RegisterCancellationHandler();

        var _runnableProjects = RunConfiguration.GetOptions().RunnableProjects;

        FileInfo[] csprojs = await AnsiConsole.Status()
            .StartAsync("Looking for projects", async ctx =>
            {
                ctx.Spinner(Spinner.Known.SimpleDotsScrolling);
                return Directory.EnumerateFiles(WorkingDirectory, "*.csproj", SearchOption.AllDirectories)
                    .Where(x => _runnableProjects.Any(y => x.EndsWith(y + ".csproj")))
                    .Select(x => new FileInfo(x))
                    .ToArray();
            });

        await console.Output.WriteLineAsync($"{csprojs.Length} csproj file(s) found.");

        if (!SkipMigration)
        {
            migrateCommand.WorkingDirectory = this.WorkingDirectory;
            migrateCommand.NoBuild = this.NoBuild;
            migrateCommand.EnvironmentName = this.EnvironmentName;

            await migrateCommand.ExecuteAsync(console);
        }

        await console.Output.WriteLineAsync("Starting projects...");

        var projects = csprojs.Where(x => !x.Name.Contains(".DbMigrator")).ToArray();

        if (!RunAll && projects.Length > 1)
        {
            await console.Output.WriteLineAsync($"\n");

            if (Projects == null || Projects.Length == 0)
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
                        .AddChoices(projects.Select(s => s.Name)));

                projects = projects.Where(x => choosedProjects.Contains(x.Name)).ToArray();
            }
            else
            {
                projects = projects.Where(x => Projects.Any(y => x.FullName.Contains(y, StringComparison.InvariantCultureIgnoreCase))).ToArray();
            }
        }

        var commandPrefix = Watch ? "watch " : string.Empty;
        var commandSuffix = NoBuild ? " --no-build" : string.Empty;

        if (GraphBuild)
        {
            commandSuffix += " /graphBuild";
        }

        if (!string.IsNullOrEmpty(Configuration))
        {
            commandSuffix += $" --configuration {Configuration}";
        }

        foreach (var csproj in projects)
        {
            var startInfo = new ProcessStartInfo("dotnet", commandPrefix + $"run --project {csproj.FullName}" + commandSuffix)
            {
                WorkingDirectory = Path.GetDirectoryName(csproj.FullName),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            if (!string.IsNullOrEmpty(EnvironmentName))
            {
                environmentManager.SetEnvironmentForProcess(EnvironmentName, startInfo);
            }

            runningProjects.Add(
                new RunningCsProjItem(
                    csproj.Name,
                    Process.Start(startInfo)
                )
            );

            if (InstallLibs)
            {
                var installLibsRunninItem = new RunningInstallLibsItem(
                    csproj.Name.Replace(".csproj", " install-libs"),
                    Process.Start(new ProcessStartInfo("abp", "install-libs")
                    {
                        WorkingDirectory = Path.GetDirectoryName(csproj.FullName),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                    })
                );

                runningProjects.Add(installLibsRunninItem);
            }
        }

        cancellationToken.Register(KillRunningProcesses);

        await RenderProcesses(cancellationToken);
    }

    private async Task RenderProcesses(CancellationToken cancellationToken)
    {
        var table = new Table().Ascii2Border();

        await AnsiConsole.Live(table)
          .StartAsync(async ctx =>
          {
              table.AddColumn("Project").AddColumn("Status");

              foreach (var project in runningProjects)
              {
                  table.AddRow(project.Name, project.Status);
              }
              ctx.Refresh();

              while (!cancellationToken.IsCancellationRequested)
              {
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
                          table.AddRow(project.Name, $"[green]*[/] {project.Status}");
                      }
                      else
                      {
                          if (project.Process.HasExited && !project.Queued)
                          {
                              project.Status = $"[red]*[/] Exited({project.Process.ExitCode})";

                              if (Retry)
                              {
                                  project.Status = $"[orange1]*[/] Exited({project.Process.ExitCode})";

                                  _ = RestartProject(project); // fire and forget
                              }
                          }
                          table.AddRow(project.Name, project.Status);
                      }
                  }

                  ctx.Refresh();
              }
          });
    }

    private static async Task RestartProject(RunningProjectItem project)
    {
        project.Queued = true;
        await Task.Delay(3100);
        project.Status = $"[orange1]*[/] Exited({project.Process.ExitCode}) (Retrying...)";
        project.Process = Process.Start(project.Process.StartInfo);
        project.StartReadingOutput();
    }

    protected void KillRunningProcesses()
    {
        console.Output.WriteLine($"- Killing running {runningProjects.Count} processes...");
        foreach (var project in runningProjects)
        {
            project.Process.Kill(entireProcessTree: true);

            project.Process.WaitForExit();
        }
    }
}