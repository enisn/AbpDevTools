using CliFx.Infrastructure;
using Spectre.Console;

namespace AbpDevTools.Commands;

[Command("clean", Description = "Cleans 'bin', 'obj' and 'node_modules' folders recursively.")]
public class CleanCommand : ICommand
{
    [CommandParameter(0, IsRequired = false, Description = "Working directory to run build. Probably project or solution directory path goes here. Default: . (Current Directory)")]
    public string WorkingDirectory { get; set; }

    private static readonly string[] foldersToDelete = new[]
    {
        Path.DirectorySeparatorChar + "bin",
        Path.DirectorySeparatorChar + "obj",
        Path.DirectorySeparatorChar + "node_modules"
    };

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }

        await AnsiConsole.Status()
            .StartAsync("Looking for directories...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.SimpleDotsScrolling);

                await Task.Yield();

                var directories = Directory.EnumerateDirectories(WorkingDirectory, string.Empty, SearchOption.AllDirectories)
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
