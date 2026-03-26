using AbpDevTools.Configuration;
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
    public string? WorkingDirectory { get; set; }

    [CommandOption("build-files", 'f', Description = "(Array) Names or part of names of projects or solutions will be built.")]
    public string[]? BuildFiles { get; set; }

    [CommandOption("interactive", 'i', Description = "Interactive build file selection.")]
    public bool Interactive { get; set; }

    [CommandOption("configuration", 'c')]
    public string? Configuration { get; set; }

    Process? runningProcess;
    protected readonly INotificationManager notificationManager;
    protected readonly ToolsConfiguration toolsConfiguration;

    public BuildCommand(INotificationManager notificationManager, ToolsConfiguration toolsConfiguration)
    {
        this.notificationManager = notificationManager;
        this.toolsConfiguration = toolsConfiguration;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }

        var cancellationToken = console.RegisterCancellationHandler();
        var buildFiles = await FindBuildFilesAsync("*.sln", "solution");

        buildFiles = buildFiles.Union(await FindBuildFilesAsync("*.slnx", "solutionx")).ToArray();

        if (buildFiles.Length == 0)
        {
            await console.Output.WriteLineAsync("No .sln/.slnx files found. Looking for .csproj files.");

            buildFiles = await FindBuildFilesAsync("*.csproj", "csproj");
        }

        if (buildFiles.Length == 0)
        {
            await console.Output.WriteLineAsync("No .csproj files found. No files to build.");

            return;
        }

        var totalStopwatch = Stopwatch.StartNew();

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

                var tools = toolsConfiguration.GetOptions();
                var stepStopwatch = Stopwatch.StartNew();
                
                try
                {
                    runningProcess = Process.Start(new ProcessStartInfo(tools["dotnet"], "build /graphBuild" + commandSuffix)
                    {
                        WorkingDirectory = Path.GetDirectoryName(buildFile.FullName),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    });

                    if (runningProcess == null)
                    {
                        stepStopwatch.Stop();
                        AnsiConsole.MarkupLine($"{progressRatio} - [red]failed[/] [bold]Building[/] {Markup.Escape(buildFile.Name)} - Could not start process [grey]in {FormatDuration(stepStopwatch.Elapsed)}[/]");
                        continue;
                    }

                    var outputTask = runningProcess.StandardOutput.ReadToEndAsync();
                    var errorTask = runningProcess.StandardError.ReadToEndAsync();
                    
                    await runningProcess.WaitForExitAsync();
                    
                    var _output = await outputTask;
                    var _error = await errorTask;

                    stepStopwatch.Stop();
                    var stepDuration = FormatDuration(stepStopwatch.Elapsed);

                    if (runningProcess.ExitCode == 0)
                    {
                        completed++;
                        AnsiConsole.MarkupLine($"{progressRatio} - [green]completed[/] [bold]Building[/] [silver]{Markup.Escape(buildFile.Name)}[/] [grey]in {stepDuration}[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"{progressRatio} - [red]failed[/] [bold]Building[/] {Markup.Escape(buildFile.Name)} Exit Code: {runningProcess.ExitCode} [grey]in {stepDuration}[/]");
                        
                        AnsiConsole.WriteLine();
                        AnsiConsole.MarkupLine($"[red]Build failed for: {Markup.Escape(buildFile.Name)}[/]");
                        
                        if (!string.IsNullOrWhiteSpace(_output))
                        {
                            AnsiConsole.MarkupLine("[grey]Standard Output:[/]");
                            AnsiConsole.WriteLine(Markup.Escape(_output));
                        }
                        
                        if (!string.IsNullOrWhiteSpace(_error))
                        {
                            AnsiConsole.MarkupLine("[red]Error Output:[/]");
                            AnsiConsole.WriteLine(Markup.Escape(_error));
                        }
                        
                        AnsiConsole.WriteLine();
                    }
                }
                catch (Exception ex)
                {
                    stepStopwatch.Stop();
                    var stepDuration = FormatDuration(stepStopwatch.Elapsed);
                    AnsiConsole.MarkupLine($"{progressRatio} - [red]failed[/] [bold]Building[/] {Markup.Escape(buildFile.Name)} - Exception: {Markup.Escape(ex.Message)} [grey]in {stepDuration}[/]");
                    AnsiConsole.WriteLine();
                }
                finally
                {
                    try
                    {
                        if (runningProcess != null && !runningProcess.HasExited)
                        {
                            runningProcess.Kill(entireProcessTree: true);
                        }
                    }
                    catch (Exception)
                    {
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }

            return completed;
        });

        totalStopwatch.Stop();
        var totalDuration = FormatDuration(totalStopwatch.Elapsed);

        AnsiConsole.MarkupLine($"[bold]Total build time:[/] [blue]{totalDuration}[/]");

        if (buildFiles.Length == 1)
        {
            await notificationManager.SendAsync("Build "+ (successfulCount > 0 ? "Completed!" : "Failed!"), $"{buildFiles[0].Name} has been built in {totalDuration}.");
        }
        else
        {
            await notificationManager.SendAsync("Build Done!", $"{successfulCount} of {buildFiles.Length} projects have been built in '{WorkingDirectory}' folder. Total time: {totalDuration}.");
        }

        cancellationToken.Register(KillRunningProcesses);
    }

    private async Task<FileInfo[]> FindBuildFilesAsync(string pattern, string? nameOfPattern = null)
    {
        nameOfPattern ??= "build";

        var files = await AnsiConsole.Status()
                .StartAsync($"Looking for {nameOfPattern} files ({pattern})", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.SimpleDotsScrolling);

                    await Task.Yield();

                    var query = Directory.EnumerateFiles(WorkingDirectory!, pattern, SearchOption.AllDirectories);

                    if (BuildFiles?.Length > 0)
                    {
                        query = query.Where(x => BuildFiles.Any(y => x.Contains(y, StringComparison.InvariantCultureIgnoreCase)));
                    }

                    var fileInfos = query
                        .Select(x => new FileInfo(x))
                        .ToArray();

                    AnsiConsole.MarkupLine($"[green]{fileInfos.Length}[/] {pattern.Replace('*', '\0')} files found.");

                    return fileInfos;
                });

        if (Interactive && files.Length > 1)
        {
            var fileChoices = files.Select(s => s.FullName.Replace(WorkingDirectory!, ".")).ToArray();
            
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
                    .AddChoices(fileChoices));

            files = files.Where(x => choosed.Contains(x.FullName.Replace(WorkingDirectory!, "."))).ToArray();
        }

        return files;
    }

    protected void KillRunningProcesses()
    {
        runningProcess?.Kill(entireProcessTree: true);

        runningProcess?.WaitForExit();
    }

    internal static string FormatDuration(TimeSpan elapsed)
    {
        if (elapsed.TotalHours >= 1)
        {
            return $"{(int)elapsed.TotalHours}h {elapsed.Minutes:D2}m {elapsed.Seconds:D2}s";
        }

        if (elapsed.TotalMinutes >= 1)
        {
            return $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds:D2}s";
        }

        var roundedSeconds = Math.Round(elapsed.TotalSeconds, 1, MidpointRounding.AwayFromZero);

        if (roundedSeconds >= 60.0)
        {
            return "1m 00s";
        }

        return roundedSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s";
    }
}
