using AbpDevTools.Configuration;
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

    [CommandOption("no-build", Description = "Skipts build before running. Passes '--no-build' parameter to dotnet run.")]
    public bool NoBuild { get; set; }

    [CommandOption("install-libs", 'i', Description = "Runs 'abp install-libs' command while running the project simultaneously.")]
    public bool InstallLibs { get; set; }

    [CommandOption("graphBuild", 'g', Description = "Uses /graphBuild while running the applications. So no need building before running. But it may cause some performance.")]
    public bool GraphBuild { get; set; }

    protected IConsole console;

    protected readonly List<RunningProjectItem> runningProjects = new();

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
            await new MigrateCommand()
            {
                WorkingDirectory = this.WorkingDirectory
            }.ExecuteAsync(console);
        }

        await console.Output.WriteLineAsync("Starting projects...");

        var projects = csprojs.Where(x => !x.Name.Contains(".DbMigrator")).ToArray();

        if (!RunAll)
        {
            await console.Output.WriteLineAsync($"\n");
            var choosedProjects = AnsiConsole.Prompt(
                new MultiSelectionPrompt<string>()
                    .Title("Choose [blueviolet]projects[/] to run.")
                    .Required(true)
                    .PageSize(12)
                    .HighlightStyle(new Style(foreground: Color.BlueViolet))
                    .MoreChoicesText("[grey](Move up and down to reveal more projects)[/]")
                    .InstructionsText(
                        "[grey](Press [blueviolet]<space>[/] to toggle a project, " +
                        "[green]<enter>[/] to accept)[/]")
                    .AddChoices(projects.Select(s => s.Name)));

            projects = projects.Where(x => choosedProjects.Contains(x.Name)).ToArray();
        }

        var commandPrefix = Watch ? "watch " : string.Empty;
        var commandPostfix = NoBuild ? " --no-build" : string.Empty;
        if (GraphBuild)
        {
            commandPostfix += " /graphBuild";
        }

        foreach (var csproj in projects)
        {
            runningProjects.Add(
                new RunningCsProjItem(
                    csproj.Name,
                    Process.Start(new ProcessStartInfo("dotnet", commandPrefix + $"run --project {csproj.FullName}" + commandPostfix)
                    {
                        WorkingDirectory = Path.GetDirectoryName(csproj.FullName),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                    })
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
                  await Task.Delay(1000);
                  table.Rows.Clear();

                  foreach (var project in runningProjects)
                  {
                      if (project.IsCompleted)
                      {
                          table.AddRow(project.Name, $"[green]*[/] {project.Status}");
                      }
                      else
                      {
                          if (project.Process.HasExited && !project.IsCompleted)
                          {
                              project.Status = $"[red]*[/] Exited({project.Process.ExitCode})";
                          }
                          table.AddRow(project.Name, project.Status);
                      }
                  }

                  ctx.Refresh();
              }
          });
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