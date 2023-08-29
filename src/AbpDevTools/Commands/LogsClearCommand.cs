using AbpDevTools.Configuration;
using CliFx.Infrastructure;
using Spectre.Console;
using System;

namespace AbpDevTools.Commands;

[Command("logs clear")]
public class LogsClearCommand : ICommand
{
    [CommandParameter(0, IsRequired = false, Description = "Working directory to run build. Probably project or solution directory path goes here. Default: . (Current Directory)")]
    public string WorkingDirectory { get; set; }

    [CommandOption("project", 'p', Description = "Determines the project to open logs of it.")]
    public string ProjectName { get; set; }

    [CommandOption("interactive", 'i', Description = "Options will be asked as prompt when this option used.")]
    public bool Interactive { get; set; }

    [CommandOption("force", 'f')]
    public bool Force { get; set; }

    protected IConsole console;

    public async ValueTask ExecuteAsync(IConsole console)
    {
        this.console = console;
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }

        var _runnableProjects = RunConfiguration.GetOptions().RunnableProjects;
        var csprojs = Directory.EnumerateFiles(WorkingDirectory, "*.csproj", SearchOption.AllDirectories)
                .Where(x => _runnableProjects.Any(y => x.EndsWith(y + ".csproj")))
                .Select(x => new FileInfo(x))
                .ToList();

        if (string.IsNullOrEmpty(ProjectName))
        {
            if (Interactive)
            {
                await console.Output.WriteLineAsync($"\n");
                ProjectName = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Choose a [blueviolet]project[/] to open logs?")
                        .PageSize(12)
                        .HighlightStyle(new Style(foreground: Color.BlueViolet))
                        .MoreChoicesText("[grey](Move up and down to reveal more rules)[/]")
                        .AddChoices(csprojs.Select(s => s.Name)));
            }
            else
            {
                await console.Output.WriteLineAsync("You have to pass a project name.\n");
                await console.Output.WriteLineAsync("\n\tUsage:");
                await console.Output.WriteLineAsync("\tlogs -p <project-name>");
                await console.Output.WriteLineAsync("\nAvailable project names:\n\n\t" +
                    string.Join("\t - ", _runnableProjects.Select(x => x.Split(Path.DirectorySeparatorChar).Last())));
                return;
            }
        }

        if (ProjectName.Equals("all", StringComparison.InvariantCultureIgnoreCase))
        {
            foreach (var csproj in csprojs)
            {
                await DeleteCsprojLogsAsync(csproj);
            }

            return;
        }

        var selectedCsproj = csprojs.FirstOrDefault(x => x.FullName.Contains(ProjectName));

        if (selectedCsproj == null)
        {
            await console.Output.WriteLineAsync($"No project found with the name '{ProjectName}'");
            return;
        }

        await DeleteCsprojLogsAsync(selectedCsproj);
    }

    protected async Task DeleteCsprojLogsAsync(FileInfo csproj)
    {
        var dir = Path.GetDirectoryName(csproj.FullName);
        var logsDir = Path.Combine(dir, "Logs");
        if (Directory.Exists(logsDir))
        {
            var filePath = Path.Combine(logsDir, "logs.txt");
            if (File.Exists(filePath))
            {
                if (!Force && !AnsiConsole.Confirm($"{filePath} will be deleted. Are you sure?"))
                {
                    return;
                }

                File.Delete(filePath);
                await console.Output.WriteLineAsync($"{filePath} deleted.");
                return;
            }
        }

        await console.Output.WriteLineAsync($"No logs found for {csproj.Name}");
    }
}
