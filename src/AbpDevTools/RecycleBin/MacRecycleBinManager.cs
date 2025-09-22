using AbpDevTools.Configuration;
using System.Diagnostics;

namespace AbpDevTools.RecycleBin;

public class MacRecycleBinManager : IRecycleBinManager
{
    protected readonly ToolsConfiguration toolsConfiguration;

    public MacRecycleBinManager(ToolsConfiguration toolsConfiguration)
    {
        this.toolsConfiguration = toolsConfiguration;
    }

    public Task SendToRecycleBinAsync(string filePath)
    {
        return SendToRecycleBinAsync(new[] { filePath });
    }

    public async Task SendToRecycleBinAsync(IEnumerable<string> filePaths)
    {
        var pathList = filePaths.ToList();
        if (!pathList.Any())
            return;

        var tools = toolsConfiguration.GetOptions();

        foreach (var filePath in pathList)
        {
            try
            {
                var absolutePath = Path.GetFullPath(filePath);
                var escapedPath = absolutePath.Replace("\"", "\\\"");
                
                var script = $"tell application \"Finder\" to move POSIX file \"{escapedPath}\" to trash";
                
                var process = Process.Start(tools["osascript"], $"-e \"{script}\"");
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode != 0)
                    {
                        throw new InvalidOperationException($"Failed to move file '{filePath}' to trash (Exit code: {process.ExitCode}). You can try --force-delete option to permanently delete files instead.");
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Failed to start osascript to move file '{filePath}' to trash. You can try --force-delete option to permanently delete files instead.");
                }
            }
            catch (InvalidOperationException)
            {
                throw; // Re-throw our own exceptions as-is
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to move file '{filePath}' to trash. You can try --force-delete option to permanently delete files instead.", ex);
            }
        }
    }
} 