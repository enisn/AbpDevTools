using System.Diagnostics;

namespace AbpDevTools.Commands;

public class RunningProjectItem
{
    public string Name { get; set; }
    public Process Process { get; set; }
    public virtual string Status { get; set; }
    public virtual bool IsCompleted { get; set; }
}

public class RunningCsProjItem : RunningProjectItem
{
    public RunningCsProjItem(string name, Process process, string status = null)
    {
        this.Name = name;
        this.Process = process;
        this.Status = status ?? "Building...";

        process.OutputDataReceived += OutputReceived;

        Process.BeginOutputReadLine();
    }

    protected virtual void OutputReceived(object sender, DataReceivedEventArgs args)
    {
        if (args.Data != null && args.Data.Contains("Now listening on: "))
        {
            Status = args.Data[args.Data.IndexOf("Now listening on: ")..];
            Process.CancelOutputRead();
            IsCompleted = true;
        }

        if (DateTime.Now - Process.StartTime > TimeSpan.FromMinutes(2))
        {
            Process.OutputDataReceived -= OutputReceived;
            Process.CancelOutputRead();
        }
    }
}

public class RunningInstallLibsItem : RunningProjectItem
{
    public RunningInstallLibsItem(string name, Process process, string status = null)
    {
        this.Name=name;
        this.Process = process;
        this.Status = status ?? "Installing...";

        process.OutputDataReceived += OutputReceived;
        process.BeginOutputReadLine();
    }

    protected virtual void OutputReceived(object sender, DataReceivedEventArgs args)
    {
        if (args.Data != null && args.Data.Contains("Done in"))
        {
            Status = "Completed.";
            Process.CancelOutputRead();
            IsCompleted = true;
        }

        if (DateTime.Now - Process.StartTime > TimeSpan.FromMinutes(5))
        {
            Process.OutputDataReceived -= OutputReceived;
            Process.CancelOutputRead();
        }
    }
}