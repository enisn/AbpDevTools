namespace AbpDevTools.RecycleBin;

public interface IRecycleBinManager
{
    Task SendToRecycleBinAsync(string filePath);
    Task SendToRecycleBinAsync(IEnumerable<string> filePaths);
} 