using AbpDevTools.Configuration;
using CliFx.Infrastructure;
using Spectre.Console;

namespace AbpDevTools.Commands;

[Command("clean", Description = "Cleans 'bin', 'obj' and 'node_modules' folders recursively.")]
public class CleanCommand : ICommand
{
    [CommandParameter(0, IsRequired = false, Description = "Working directory to run build. Probably project or solution directory path goes here. Default: . (Current Directory)")]
    public string? WorkingDirectory { get; set; }

    private readonly CleanConfiguration cleanConfiguration;

    public CleanCommand(CleanConfiguration cleanConfiguration)
    {
        this.cleanConfiguration = cleanConfiguration;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }

        var foldersToDelete = cleanConfiguration.GetOptions()
            .Folders.Select(x => Path.DirectorySeparatorChar + x)
            .ToArray();

        await AnsiConsole.Status()
            .StartAsync("Looking for directories...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.SimpleDotsScrolling);

                await Task.Yield();

                var directories = Directory.EnumerateDirectories(WorkingDirectory!, string.Empty, SearchOption.AllDirectories)
                    .Where(x => foldersToDelete.Any(a => x.EndsWith(a)));

                foreach (var directory in directories)
                {
                    ctx.Status($"Deleting {directory}...");
                    Directory.Delete(directory, true);
                }
            });

        console.Output.WriteLine("Cleaned successfully.");
    }
}
