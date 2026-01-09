namespace AbpDevTools.Processes;

public interface IProcessFinder
{
    Task<List<ProcessInfo>> FindProcessesByPortAsync(int port);
}

public class ProcessInfo
{
    public required int Pid { get; set; }
    public required string ProcessName { get; set; }
    public required string Path { get; set; }
}
