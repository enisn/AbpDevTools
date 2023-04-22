using CliFx.Infrastructure;
using Spectre.Console;
using System.Diagnostics;

namespace AbpDevTools.Commands;

[Command("build", Description = "Shortcut for dotnet build /graphBuild")]
public class BuildCommand : ICommand
{
    [CommandParameter(0, IsRequired = false, Description = "Working directory to run build. Probably project or solution directory path goes here. Default: . (Current Directory)")]
    public string WorkingDirectory { get; set; }

    Process runningProcess;

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }

        var cancellationToken = console.RegisterCancellationHandler();

        var buildFiles = await AnsiConsole.Status()
           .StartAsync("Looking for solution files (.sln)", async ctx =>
           {
               ctx.Spinner(Spinner.Known.SimpleDotsScrolling);
               var slns = Directory.EnumerateFiles(WorkingDirectory, "*.sln", SearchOption.AllDirectories)
                   .Select(x => new FileInfo(x))
                   .ToArray();

               AnsiConsole.MarkupLine($"[green]{slns.Length}[/] .sln files found.");

               return slns;
           });

        if (buildFiles.Length == 0)
        {
            await console.Output.WriteLineAsync("No .sln files found. Looking for .csproj files.");

            buildFiles = await AnsiConsole.Status()
                .StartAsync("Looging for C# Projects (.csproj)", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.SimpleDotsScrolling);
                    var csprojs = Directory.EnumerateFiles(WorkingDirectory, "*.csproj", SearchOption.AllDirectories)
                        .Select(x => new FileInfo(x))
                        .ToArray();

                    AnsiConsole.MarkupLine($"[green]{csprojs.Length}[/] .sln files found.");
                    return csprojs;
                });
        }

        await AnsiConsole.Status().StartAsync("Starting build...", async ctx =>
        {
            for (int i = 0; i < buildFiles.Length; i++)
            {
                var buildFile = buildFiles[i];
                var progressRatio = $"[yellow]{i + 1}/{buildFiles.Length}[/]";
                ctx.Status($"{progressRatio} - [bold]Building[/] {buildFile.FullName}");

                runningProcess = Process.Start(new ProcessStartInfo("dotnet", "build /graphBuild")
                {
                    WorkingDirectory = Path.GetDirectoryName(buildFile.FullName),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                });

                // equivalent of WaitforExit
                var _output = await runningProcess.StandardOutput.ReadToEndAsync();

                if (runningProcess.ExitCode == 0)
                {
                    AnsiConsole.MarkupLine($"{progressRatio} - [bold]Building[/] [silver]{buildFile.FullName}[/] [green]completed[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"{progressRatio} - [red][bold]Building[/] {buildFile.Name} failed. Exit Code: {runningProcess.ExitCode}[/]");
                    AnsiConsole.MarkupLine($"[grey]{_output}[/]");
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        });

        cancellationToken.Register(() =>
        {
            runningProcess.Kill(entireProcessTree: true);
        });
    }
}
