using AbpDevTools.Commands;
using AbpDevTools.Environments;
using CliFx.Infrastructure;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace AbpDevTools.Running;

[Command("up", Description = "Run applications from configuration.")]
public class RunFromConfigurationCommand : ICommand
{
    [CommandParameter(0, IsRequired = false, Description = "Working directory to run build. Probably project or solution directory path goes here. Default: . (Current Directory)")]
    public string WorkingDirectory { get; set; }

    [CommandOption("file", 'f', Description = "Name of the yaml configuration file. Default: .abpdevrun.yml")]
    public string FileName { get; set; } = ".abpdevrun.yml";

    protected readonly List<RunningProjectItem> runningProjects = new();
    protected readonly IDeserializer yamlDeserializer;
    protected readonly IProcessEnvironmentManager environmentManager;
    protected IConsole console;
    protected RunningConfiguration runConfiguration;

    public RunFromConfigurationCommand(IDeserializer yamlDeserializer, IProcessEnvironmentManager environmentManager)
    {
        this.yamlDeserializer = yamlDeserializer;
        this.environmentManager = environmentManager;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        this.console = console;
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }
        
        using var fileStream = File.OpenText(Path.Combine(WorkingDirectory, FileName));

        runConfiguration = yamlDeserializer.Deserialize<RunningConfiguration>(fileStream);

        var cancellationToken = console.RegisterCancellationHandler();

        foreach (var project in runConfiguration.Projects.Values)
        {
            var commandPrefix = (project.Watch ?? runConfiguration.Watch ) ? "watch " : string.Empty;
            var commandSuffix = (project.NoBuild ?? runConfiguration.NoBuild )? " --no-build" : string.Empty;

            if ((project.GraphBuild ?? runConfiguration.GraphBuild))
            {
                commandSuffix += " /graphBuild";
            }

            if (!string.IsNullOrEmpty((project.Configuration ?? runConfiguration.Configuration)))
            {
                commandSuffix += $" --configuration {project.Configuration}";
            }


            var startInfo = new ProcessStartInfo("dotnet", commandPrefix + $"run --project {project.Path}" + commandSuffix)
            {
                WorkingDirectory = Path.GetDirectoryName(project.Path),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            if (!string.IsNullOrEmpty((project.EnvironmentName ?? runConfiguration.EnvironmentName)))
            {
                environmentManager.SetEnvironmentForProcess((project.EnvironmentName ?? runConfiguration.EnvironmentName), startInfo);
            }

            runningProjects.Add(
                new RunningCsProjItem(
                    project.Path,
                    Process.Start(startInfo)
                )
            );

            if ((project.InstallLibs ?? runConfiguration.InstallLibs))
            {
                var installLibsRunninItem = new RunningInstallLibsItem(
                    project.Path.Replace(".csproj", " install-libs"),
                    Process.Start(new ProcessStartInfo("abp", "install-libs")
                    {
                        WorkingDirectory = Path.GetDirectoryName(project.Path),
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

                              if (runConfiguration.Retry)
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
