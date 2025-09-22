using AbpDevTools.Configuration;
using System.Diagnostics;

namespace AbpDevTools.RecycleBin;

public class LinuxRecycleBinManager : IRecycleBinManager
{
    protected readonly ToolsConfiguration toolsConfiguration;

    public LinuxRecycleBinManager(ToolsConfiguration toolsConfiguration)
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
                
                var process = Process.Start(tools.ContainsKey("gio") ? tools["gio"] : "gio", $"trash \"{absolutePath}\"");
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
                    throw new InvalidOperationException($"Failed to start gio trash command for file '{filePath}'. You can try --force-delete option to permanently delete files instead.");
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