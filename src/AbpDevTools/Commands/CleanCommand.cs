using AbpDevTools.Configuration;
using AbpDevTools.RecycleBin;
using CliFx.Infrastructure;
using Spectre.Console;
using System.Text;

namespace AbpDevTools.Commands;

[Command("clean", Description = "Cleans 'bin', 'obj' and 'node_modules' folders recursively.")]
public class CleanCommand : ICommand
{
    [CommandParameter(0, IsRequired = false, Description = "Working directory to run build. Probably project or solution directory path goes here. Default: . (Current Directory)")]
    public string? WorkingDirectory { get; set; }

    [CommandOption("soft-delete", 's', Description = "Move to recycle bin instead of permanent deletion.")]
    public bool SoftDelete { get; set; }

    [CommandOption("ignore-path", 'i', Description = "Skip path from cleaning.")]
    public string[] IgnorePaths { get; set; } = Array.Empty<string>();

    private readonly CleanConfiguration cleanConfiguration;
    private readonly IRecycleBinManager recycleBinManager;

    public CleanCommand(CleanConfiguration cleanConfiguration, IRecycleBinManager recycleBinManager)
    {
        this.cleanConfiguration = cleanConfiguration;
        this.recycleBinManager = recycleBinManager;
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

        var errorResults = new List<(string Path, Exception Error)>();
        var successCount = 0;
        var totalCount = 0;

        await AnsiConsole.Status()
            .StartAsync("Looking for directories...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.SimpleDotsScrolling);

                await Task.Yield();

                var allDirectories = Directory.EnumerateDirectories(WorkingDirectory!, string.Empty, SearchOption.AllDirectories)
                    .Where(x => foldersToDelete.Any(a => x.EndsWith(a)) && !IgnorePaths.Any(a => x.Contains(a)))
                    .ToList();

                // Optimize: Remove directories that are subdirectories of other directories to be deleted
                var optimizedDirectories = OptimizeDirectoryList(allDirectories);
                totalCount = optimizedDirectories.Count;

                if (optimizedDirectories.Any())
                {
                    ctx.Status($"Processing {optimizedDirectories.Count} directories...");

                    foreach (var directory in optimizedDirectories)
                    {
                        try
                        {
                            ctx.Status($"Processing {directory}");
                            
                            if (SoftDelete)
                            {
                                await recycleBinManager.SendToRecycleBinAsync(new[] { directory });
                            }
                            else
                            {
                                Directory.Delete(directory, true);
                            }
                            
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            errorResults.Add((directory, ex));
                        }
                    }
                }
            });

        // Display results
        DisplayResults(console, successCount, totalCount, errorResults);
    }

    private static List<string> OptimizeDirectoryList(List<string> directories)
    {
        // Sort directories by path length (shorter paths first)
        var sortedDirectories = directories.OrderBy(d => d.Length).ToList();
        var optimizedDirectories = new List<string>();

        foreach (var directory in sortedDirectories)
        {
            // Check if this directory is a subdirectory of any already selected directory
            var isSubdirectory = optimizedDirectories.Any(selectedDir => 
                directory.StartsWith(selectedDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

            if (!isSubdirectory)
            {
                optimizedDirectories.Add(directory);
            }
        }

        return optimizedDirectories;
    }

    private static void DisplayResults(IConsole console, int successCount, int totalCount, List<(string Path, Exception Error)> errorResults)
    {
        var resultBuilder = new StringBuilder();

        if (errorResults.Any())
        {
            resultBuilder.AppendLine($"⚠️  Clean operation completed with errors:");
            resultBuilder.AppendLine($"✅ Successfully processed: {successCount}/{totalCount} directories");
            resultBuilder.AppendLine($"❌ Failed: {errorResults.Count} directories");
            resultBuilder.AppendLine();
            
            resultBuilder.AppendLine("Error details:");
            foreach (var (path, error) in errorResults)
            {
                resultBuilder.AppendLine($"  • {path}");
                resultBuilder.AppendLine($"    Error: {error.Message}");
                resultBuilder.AppendLine();
            }
        }
        else
        {
            resultBuilder.AppendLine($"✅ Clean operation completed successfully!");
            resultBuilder.AppendLine($"Processed {successCount} directories");
        }

        console.Output.WriteLine(resultBuilder.ToString());
    }
}
