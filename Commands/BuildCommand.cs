using CliFx.Infrastructure;
using Spectre.Console;
using System;
using System.Diagnostics;

namespace AbpDevTools.Commands;

[Command("build", Description = "Shortcut for dotnet build /graphBuild")]
public class BuildCommand : ICommand
{
    [CommandParameter(0, IsRequired = false, Description = "Working directory to run build. Probably project or solution directory path goes here. Default: . (Current Directory)")]
    public string WorkingDirectory { get; set; }

    [CommandOption("build-files", 'f', Description = "(Array) Names or part of names of projects or solutions will be built.")]
    public string[] BuildFiles { get; set; }

    Process runningProcess;

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }

        var cancellationToken = console.RegisterCancellationHandler();
        var buildFiles = await FindSolutionBuildFilesAsync();

        if (buildFiles.Length == 0)
        {
            await console.Output.WriteLineAsync("No .sln files found. Looking for .csproj files.");

            buildFiles = await FindCsprojBuildFilesAsync(buildFiles);
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
                await runningProcess.WaitForExitAsync();

                if (runningProcess.ExitCode == 0)
                {
                    AnsiConsole.MarkupLine($"{progressRatio} - [green]completed[/] [bold]Building[/] [silver]{buildFile.Name}[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"{progressRatio} - [red]failed [bold]Building[/] {buildFile.Name} Exit Code: {runningProcess.ExitCode}[/]");
                    AnsiConsole.MarkupLine($"[grey]{_output}[/]");
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                runningProcess.Kill(entireProcessTree: true);
            }
        });

        cancellationToken.Register(KillRunningProcesses);
    }

    private async Task<FileInfo[]> FindSolutionBuildFilesAsync()
    {
        return await AnsiConsole.Status()
                .StartAsync("Looking for solution files (.sln)", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.SimpleDotsScrolling);
                    var query = Directory.EnumerateFiles(WorkingDirectory, "*.sln", SearchOption.AllDirectories);

                    if (BuildFiles?.Length > 0)
                    {
                        query = query.Where(x => BuildFiles.Any(y => x.Contains(y, StringComparison.InvariantCultureIgnoreCase)));
                    }

                    var slns = query
                        .Select(x => new FileInfo(x))
                        .ToArray();

                    AnsiConsole.MarkupLine($"[green]{slns.Length}[/] .sln files found.");

                    return slns;
                });
    }

    private async Task<FileInfo[]> FindCsprojBuildFilesAsync(FileInfo[] buildFiles)
    {
        buildFiles = await AnsiConsole.Status()
            .StartAsync("Looging for C# Projects (.csproj)", async ctx =>
            {
                ctx.Spinner(Spinner.Known.SimpleDotsScrolling);
                var query = Directory.EnumerateFiles(WorkingDirectory, "*.csproj", SearchOption.AllDirectories);

                if (BuildFiles?.Length > 0)
                {
                    query = query.Where(x => BuildFiles.Any(y => x.Contains(y, StringComparison.InvariantCultureIgnoreCase)));
                }

                var csprojs = query
                    .Select(x => new FileInfo(x))
                    .ToArray();

                AnsiConsole.MarkupLine($"[green]{csprojs.Length}[/] .csproj files found.");
                return csprojs;
            });
        return buildFiles;
    }

    protected void KillRunningProcesses()
    {
        runningProcess.Kill(entireProcessTree: true);

        runningProcess.WaitForExit();
    }
}
