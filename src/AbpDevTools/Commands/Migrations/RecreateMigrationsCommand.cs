using AbpDevTools.Configuration;
using AbpDevTools.Services;
using CliFx.Infrastructure;
using Spectre.Console;
using System.Diagnostics;

namespace AbpDevTools.Commands.Migrations;

[Command("migrations recreate", Description = "Clears existing migrations and recreates an 'Initial' migration for selected EntityFrameworkCore project(s). Optionally drops databases afterwards.")]
public class RecreateMigrationsCommand : MigrationsCommandBase, ICommand
{
    [CommandOption("drop-database", Description = "Drop databases after recreating migrations (runs EF 'database drop' for selected projects).")]
    public bool DropDatabase { get; set; }

    public string MigrationName { get; set; } = "Initial";

    private readonly ToolsConfiguration toolsConfiguration;

    public RecreateMigrationsCommand(
        EntityFrameworkCoreProjectsProvider entityFrameworkCoreProjectsProvider,
        ToolsConfiguration toolsConfiguration)
        : base(entityFrameworkCoreProjectsProvider)
    {
        this.toolsConfiguration = toolsConfiguration;
    }

    public override async ValueTask ExecuteAsync(IConsole console)
    {
        await base.ExecuteAsync(console);

        var cancellationToken = console.RegisterCancellationHandler();

        var projectFiles = await ChooseProjectsAsync();

        if (projectFiles.Length == 0)
        {
            await console.Output.WriteLineAsync("No EF Core projects found. Nothing to recreate.");
            return;
        }

        await AnsiConsole.Status().StartAsync("Recreating migrations...", async ctx =>
        {
            ctx.Spinner(Spinner.Known.SimpleDotsScrolling);

            // 1) Clear migrations
            foreach (var project in projectFiles)
            {
                var migrationsFolder = Path.Combine(Path.GetDirectoryName(project.FullName)!, "Migrations");
                if (Directory.Exists(migrationsFolder))
                {
                    Directory.Delete(migrationsFolder, true);
                    AnsiConsole.MarkupLine($"[green]Cleared[/] migrations of [bold]{Path.GetFileNameWithoutExtension(project.Name)}[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"No migrations found in [bold]{Path.GetFileNameWithoutExtension(project.Name)}[/]");
                }
            }

            // 2) Add Initial migration
            foreach (var project in projectFiles)
            {
                var arguments = $"migrations add {MigrationName} --project {project.FullName}";
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo("dotnet-ef", arguments)
                    {
                        WorkingDirectory = WorkingDirectory,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                var projectName = Path.GetFileNameWithoutExtension(project.Name);
                AnsiConsole.MarkupLine($"[blue]Adding migration[/] [bold]{MigrationName}[/] for [bold]{projectName}[/]...");

                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                    {
                        AnsiConsole.MarkupLineInterpolated($"[grey]* {Markup.Escape(e.Data)}[/]");
                    }
                };

                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                    {
                        AnsiConsole.MarkupLineInterpolated($"[red]* {Markup.Escape(e.Data)}[/]");
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode == 0)
                {
                    AnsiConsole.MarkupLine($"[green]Completed[/] adding migration for [bold]{projectName}[/].");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Failed[/] adding migration for [bold]{projectName}[/]. Exit code: {process.ExitCode}");
                }
            }

            // 3) Optionally drop databases (per selected project)
            if (DropDatabase)
            {
                var tools = toolsConfiguration.GetOptions();
                foreach (var project in projectFiles)
                {
                    var projectDir = Path.GetDirectoryName(project.FullName)!;
                    var projectName = Path.GetFileNameWithoutExtension(project.Name);

                    AnsiConsole.MarkupLine($"[blue]Dropping database for[/] [bold]{projectName}[/]...");

                    var startInfo = new ProcessStartInfo(tools["dotnet"], "ef database drop --force")
                    {
                        WorkingDirectory = projectDir,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = false
                    };

                    var dropProcess = Process.Start(startInfo)!;

                    dropProcess.OutputDataReceived += async (_, args) =>
                    {
                        if (args?.Data != null)
                        {
                            await console.Output.WriteLineAsync("* " + args.Data);
                        }
                    };

                    dropProcess.BeginOutputReadLine();
                    await dropProcess.WaitForExitAsync(cancellationToken);
                }
            }
        });
    }
}


