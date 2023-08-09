using AbpDevTools.Notifications;
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

    [CommandOption("interactive", 'i', Description = "Interactive build file selection.")]
    public bool Interactive { get; set; }

    [CommandOption("configuration", 'c')]
    public string Configuration { get; set; }

    Process runningProcess;
    protected readonly INotificationManager notificationManager;

    public BuildCommand(INotificationManager notificationManager)
    {
        this.notificationManager = notificationManager;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }

        var cancellationToken = console.RegisterCancellationHandler();
        var buildFiles = await FindBuildFilesAsync("*.sln", "solution");

        if (buildFiles.Length == 0)
        {
            await console.Output.WriteLineAsync("No .sln files found. Looking for .csproj files.");

            buildFiles = await FindBuildFilesAsync("*.csproj", "csproj");
        }

        if (buildFiles.Length == 0)
        {
            await console.Output.WriteLineAsync("No .csproj files found. No files to build.");

            return;
        }

        var successfulCount = await AnsiConsole.Status().StartAsync("Starting build...", async ctx =>
        {
            int completed = 0;
            for (int i = 0; i < buildFiles.Length; i++)
            {
                var buildFile = buildFiles[i];
                var progressRatio = $"[yellow]{i + 1}/{buildFiles.Length}[/]";
                ctx.Status($"{progressRatio} - [bold]Building[/] {buildFile.FullName}");

                var commandSuffix = string.Empty;

                if (!string.IsNullOrEmpty(Configuration))
                {
                    commandSuffix += $" --configuration {Configuration}";
                }

                runningProcess = Process.Start(new ProcessStartInfo("dotnet", "build /graphBuild" + commandSuffix)
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
                    completed++;
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

            return completed;
        });

        if (buildFiles.Length == 1)
        {
            await notificationManager.SendAsync("Build "+ (successfulCount > 0 ? "Completed!" : "Failed!"), $"{buildFiles[0].Name} has been built.");
        }
        else
        {
            await notificationManager.SendAsync("Build Done!", $"{successfulCount} of {buildFiles.Length} projects have been built in '{WorkingDirectory}' folder.");
        }

        cancellationToken.Register(KillRunningProcesses);
    }

    private async Task<FileInfo[]> FindBuildFilesAsync(string pattern, string nameOfPattern = null)
    {
        nameOfPattern ??= "build";

        var files = await AnsiConsole.Status()
                .StartAsync($"Looking for {nameOfPattern} files ({pattern})", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.SimpleDotsScrolling);
                    var query = Directory.EnumerateFiles(WorkingDirectory, pattern, SearchOption.AllDirectories);

                    if (BuildFiles?.Length > 0)
                    {
                        query = query.Where(x => BuildFiles.Any(y => x.Contains(y, StringComparison.InvariantCultureIgnoreCase)));
                    }

                    var slns = query
                        .Select(x => new FileInfo(x))
                        .ToArray();

                    AnsiConsole.MarkupLine($"[green]{slns.Length}[/] {pattern.Replace('*', '\0')} files found.");

                    return slns;
                });

        if (Interactive && files.Length > 1)
        {
            var choosed = AnsiConsole.Prompt(
                new MultiSelectionPrompt<string>()
                    .Title("Choose files to be built:")
                    .NotRequired() // Not required to have a favorite fruit
                    .PageSize(12)
                    .HighlightStyle(new Style(foreground: Color.MediumPurple2))
                    .MoreChoicesText("[grey](Move up and down to reveal more files)[/]")
                    .InstructionsText(
                        "[grey](Press [mediumpurple2]<space>[/] to toggle a file, " +
                        "[green]<enter>[/] to accept)[/]")
                    .AddChoices(files.Select(s => s.FullName)));

            files = files.Where(x => choosed.Contains(x.FullName)).ToArray();
        }

        return files;
    }

    protected void KillRunningProcesses()
    {
        runningProcess.Kill(entireProcessTree: true);

        runningProcess.WaitForExit();
    }
}
