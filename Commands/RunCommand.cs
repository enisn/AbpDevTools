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

    protected IConsole console;

    protected readonly List<RunningProjectItem> runningProjects = new();

    private static readonly string[] _runnableProjects = new string[]{
        ".HttpApi.Host",
        ".HttpApi.HostWithIds",
        ".AuthServer",
        ".Web",
        ".Web.Host",
        ".Blazor",
        ".Blazor.Host",
        ".Blazor.Server",
        ".Blazor.Server.Host",
        ".Blazor.Server.Tiered",
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

        await RenderProcesses(cancellationToken);

        foreach (var project in runningProjects)
        {
            project.Process.Kill();
        }
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
              ctx.Refresh();

              while (!cancellationToken.IsCancellationRequested)
              {
                  await Task.Delay(1000);
                  table.Rows.Clear();

                  foreach (var project in runningProjects)
                  {
                      if (project.IsRunning)
                      {
                          table.AddRow(project.Name, $"[green]*[/] {project.Status}");
                      }
                      else
                      {
                          if (project.Process.HasExited)
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
}