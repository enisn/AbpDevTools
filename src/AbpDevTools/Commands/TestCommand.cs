using CliFx.Infrastructure;
using Spectre.Console;
using System.Diagnostics;

namespace AbpDevTools.Commands;

[Command("test", Description = "runs 'dotnet test' command recursively.")]
public class TestCommand : ICommand
{
    [CommandParameter(0, IsRequired = false, Description = "Working directory to run test. Probably project or solution directory path goes here. Default: . (Current Directory)")]
    public string WorkingDirectory { get; set; }

    [CommandOption("files", 'f', Description = "(Array) Names or part of names of solutions will be tested.")]
    public string[] TestFiles { get; set; }

    [CommandOption("interactive", 'i', Description = "Interactive test solution selection.")]
    public bool Interactive { get; set; }

    [CommandOption("configuration", 'c')]
    public string Configuration { get; set; }

    [CommandOption("no-build", Description = "Skips build before running. Passes '--no-build' parameter to dotnet test.")]
    public bool NoBuild { get; set; }

    protected IConsole console;
    protected Process runningProcess;

    public async ValueTask ExecuteAsync(IConsole console)
    {
        this.console = console;
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }
        var cancellationToken = console.RegisterCancellationHandler();

        cancellationToken.Register(() =>
        {
            AnsiConsole.MarkupLine("[red]AbpDev Test cancelled by the user.[/]");
            console.Output.WriteLine("Killing process with id " + runningProcess.Id);
            runningProcess.Kill(true);
        });

        var buildFiles = await FindBuildFilesAsync("*.sln", "solution");

        if (buildFiles.Length == 0)
        {
            await console.Output.WriteLineAsync("No .sln files found. Looking for .csproj files.");
            return;
        }

        var successfulCount = await AnsiConsole.Status().StartAsync("Starting tests...", async ctx =>
        {
            int completed = 0;
            for (int i = 0; i < buildFiles.Length; i++)
            {
                var buildFile = buildFiles[i];

                var commandSuffix = NoBuild ? " --no-build" : string.Empty;
                if (!string.IsNullOrEmpty(Configuration))
                {
                    commandSuffix += $" --configuration {Configuration}";
                }

                var startInfo = new ProcessStartInfo("dotnet", $"test {buildFile.FullName}{commandSuffix}");
                startInfo.RedirectStandardOutput = true;
                startInfo.WorkingDirectory = WorkingDirectory;

                runningProcess = Process.Start(startInfo);
                ctx.Status($"Running tests for {buildFile.Name}.");
                runningProcess.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        AnsiConsole.MarkupLine($"[grey]{e.Data}[/]");
                    }
                };
                runningProcess.BeginOutputReadLine();

                await runningProcess.WaitForExitAsync(cancellationToken);

                if (runningProcess.ExitCode == 0)
                {
                    completed++;
                }
            }

            return completed;
        });
    }

    private async Task<FileInfo[]> FindBuildFilesAsync(string pattern, string nameOfPattern = null)
    {
        nameOfPattern ??= "solution";

        var files = await AnsiConsole.Status()
                .StartAsync($"Looking for {nameOfPattern} files ({pattern})", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.SimpleDotsScrolling);
                    var query = Directory.EnumerateFiles(WorkingDirectory, pattern, SearchOption.AllDirectories);

                    if (TestFiles?.Length > 0)
                    {
                        query = query.Where(x => TestFiles.Any(y => x.Contains(y, StringComparison.InvariantCultureIgnoreCase)));
                    }

                    var fileInfo = query
                        .Select(x => new FileInfo(x))
                        .ToArray();

                    AnsiConsole.MarkupLine($"[green]{fileInfo.Length}[/] {pattern.Replace('*', '\0')} files found.");

                    return fileInfo;
                });

        if (Interactive && files.Length > 1)
        {
            var choosed = AnsiConsole.Prompt(
                new MultiSelectionPrompt<string>()
                    .Title("Choose files to be tested:")
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
}
