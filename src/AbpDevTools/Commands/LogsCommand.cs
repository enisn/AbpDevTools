using AbpDevTools.Services;
using CliFx.Infrastructure;
using Spectre.Console;

namespace AbpDevTools.Commands;

[Command("logs", Description = "Print the last lines of project logs or open them with the operating system default app.")]
public class LogsCommand : ICommand
{
    private const int DefaultTailLineCount = 100;

    [CommandParameter(0, Description = "Determines the project to open logs of it.", IsRequired = false)]
    public string? ProjectName { get; set; }

    [CommandOption("path", 'p', Description = "Working directory of the command. Probably solution directory. Default: . (CurrentDirectory) ")]
    public string? WorkingDirectory { get; set; }

    [CommandOption("interactive", 'i', Description = "Options will be asked as prompt when this option used.")]
    public bool Interactive { get; set; }

    [CommandOption("open", 'o', Description = "Open the log file or folder with the operating system default app instead of printing log lines.")]
    public bool OpenWithDefaultApp { get; set; }

    [CommandOption("lines", 'n', Description = "Number of lines to print from the end of logs.txt when not using --open. Default: 100.")]
    public int Lines { get; set; } = DefaultTailLineCount;

    protected readonly RunnableProjectsProvider runnableProjectsProvider;
    protected readonly Platform platform;

    public LogsCommand(RunnableProjectsProvider runnableProjectsProvider, Platform platform)
    {
        this.runnableProjectsProvider = runnableProjectsProvider;
        this.platform = platform;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }

        var csprojs = runnableProjectsProvider.GetRunnableProjects(WorkingDirectory);

        if (string.IsNullOrEmpty(ProjectName))
        {
            if (Interactive)
            {
                await console.Output.WriteLineAsync($"\n");
                ProjectName = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Choose a [mediumpurple2]project[/] to open logs?")
                        .PageSize(12)
                        .HighlightStyle(new Style(foreground: Color.MediumPurple2))
                        .MoreChoicesText("[grey](Move up and down to reveal more rules)[/]")
                        .AddChoices(csprojs.Select(s => s.Name)));
            }
            else
            {
                await console.Output.WriteLineAsync("You have to pass a project name.\n");
                await console.Output.WriteLineAsync("\n\tUsage:");
                await console.Output.WriteLineAsync("\tabpdev logs <project-name>");
                await console.Output.WriteLineAsync("\tabpdev logs <project-name> -n 100");
                await console.Output.WriteLineAsync("\tabpdev logs <project-name> --open");
                await console.Output.WriteLineAsync("\nAvailable project names:\n\n\t - " +
                    string.Join("\n\t - ", csprojs.Select(x => x.Name.Split(Path.DirectorySeparatorChar).Last())));
                return;
            }
        }

        if (Lines <= 0)
        {
            await console.Error.WriteLineAsync("The '--lines' option must be greater than 0.");
            return;
        }

        var selectedCsproj = csprojs.FirstOrDefault(x => x.FullName.Contains(ProjectName, StringComparison.InvariantCultureIgnoreCase));

        if (selectedCsproj == null)
        {
            await console.Output.WriteLineAsync($"No project found with the name '{ProjectName}'");
            return;
        }

        var dir = Path.GetDirectoryName(selectedCsproj.FullName)!;
        var logsDir = Path.Combine(dir, "Logs");
        var filePath = Path.Combine(logsDir, "logs.txt");

        if (OpenWithDefaultApp)
        {
            if (Directory.Exists(logsDir))
            {
                if (File.Exists(filePath))
                {
                    platform.Open(filePath);
                }
                else
                {
                    platform.Open(logsDir);
                }
            }
            else
            {
                await console.Output.WriteLineAsync("No logs folder found for project.\nOpening project folder...");

                platform.Open(dir);
            }

            return;
        }

        if (!Directory.Exists(logsDir))
        {
            await console.Output.WriteLineAsync($"No logs folder found for project '{selectedCsproj.Name}'. Use '--open' to inspect the project folder.");
            return;
        }

        if (!File.Exists(filePath))
        {
            await console.Output.WriteLineAsync($"No logs file found for project '{selectedCsproj.Name}' at '{filePath}'. Use '--open' to inspect the Logs folder.");
            return;
        }

        var lastLines = await ReadLastLinesAsync(filePath, Lines);

        if (lastLines.Count == 0)
        {
            await console.Output.WriteLineAsync($"Log file is empty: {filePath}");
            return;
        }

        await console.Output.WriteLineAsync($"Showing last {lastLines.Count} line(s) from '{filePath}':");
        foreach (var line in lastLines)
        {
            await console.Output.WriteLineAsync(line);
        }
    }

    private static async Task<IReadOnlyList<string>> ReadLastLinesAsync(string filePath, int lineCount)
    {
        var lines = new Queue<string>(lineCount);

        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(fileStream);

        while (await reader.ReadLineAsync() is { } line)
        {
            if (lines.Count == lineCount)
            {
                lines.Dequeue();
            }

            lines.Enqueue(line);
        }

        return lines.ToArray();
    }
}
