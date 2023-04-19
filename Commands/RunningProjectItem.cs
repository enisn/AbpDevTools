using System.Diagnostics;

namespace AbpDevTools.Commands;

public class RunningProjectItem
{
    public string Name { get; set; }
    public Process Process { get; set; }
    public string Status { get; set; }
    public bool IsRunning { get; set; }
}