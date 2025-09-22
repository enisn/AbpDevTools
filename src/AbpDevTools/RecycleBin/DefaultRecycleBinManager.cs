using Spectre.Console;

namespace AbpDevTools.RecycleBin;

public class DefaultRecycleBinManager : IRecycleBinManager
{
    public Task SendToRecycleBinAsync(string filePath)
    {
        return SendToRecycleBinAsync(new[] { filePath });
    }

    public Task SendToRecycleBinAsync(IEnumerable<string> filePaths)
    {
        AnsiConsole.MarkupLine("[yellow]Warning: Recycle bin not supported on this platform. Files will be permanently deleted.[/]");
        
        foreach (var filePath in filePaths)
        {
            try
            {
                if (Directory.Exists(filePath))
                {
                    Directory.Delete(filePath, true);
                }
                else if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to delete file or directory '{filePath}'. You can check file permissions or if the file is in use.", ex);
            }
        }
        
        return Task.CompletedTask;
    }
} 