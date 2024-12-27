using System.Diagnostics;

namespace AbpDevTools.Commands;

public class RunningProjectItem
{
    public string? Name { get; set; }
    public Process? Process { get; set; }
    public virtual string? Status { get; set; }
    public virtual bool IsCompleted { get; set; }
    public virtual bool Queued { get; set; }
    public bool Verbose { get; set; }

    public virtual void StartReadingOutput()
    {
    }
}

public class RunningCsProjItem : RunningProjectItem
{
    public RunningCsProjItem(string name, Process process, string? status = null, bool verbose = false)
    {
        this.Name = name;
        this.Process = process;
        this.Status = status ?? "Building...";
        this.Verbose = verbose;
        StartReadingOutput();
    }

    public override void StartReadingOutput()
    {
        Queued = false;
        Process!.OutputDataReceived -= OutputReceived;
        Process!.OutputDataReceived += OutputReceived;
        Process!.BeginOutputReadLine();
    }

    protected virtual void OutputReceived(object sender, DataReceivedEventArgs args)
    {
        if (!IsCompleted && Verbose)
        {
            Status = args.Data?.Replace("[", string.Empty).Replace("]", string.Empty) ?? string.Empty;
        }

        if (args.Data != null && args.Data.Contains("Now listening on: "))
        {
            Status = args.Data[args.Data.IndexOf("Now listening on: ")..];
            Process?.CancelOutputRead();
            IsCompleted = true;
        }

        if (args.Data != null && 
            args.Data.Contains("dotnet watch ") &&
            args.Data.Contains(" Started"))
        {
            Status = args.Data;
            Process?.CancelOutputRead();
            IsCompleted = true;
        }

        if (DateTime.Now - Process?.StartTime > TimeSpan.FromMinutes(5))
        {
            Status = "Stale";
            Process!.OutputDataReceived -= OutputReceived;
            Process.CancelOutputRead();
        }
    }
}

public class RunningInstallLibsItem : RunningProjectItem
{
    public RunningInstallLibsItem(string name, Process process, string? status = null)
    {
        this.Name = name;
        this.Process = process;
        this.Status = status ?? "Installing...";
        StartReadingOutput();
    }

    public override void StartReadingOutput()
    {
        Process!.OutputDataReceived -= OutputReceived;
        Process!.OutputDataReceived += OutputReceived;
        Process!.BeginOutputReadLine();
    }

    protected virtual void OutputReceived(object sender, DataReceivedEventArgs args)
    {
        if (args.Data != null && args.Data.Contains("Done in"))
        {
            Status = "Completed.";
            Process!.CancelOutputRead();
            IsCompleted = true;
        }

        if (DateTime.Now - Process!.StartTime > TimeSpan.FromMinutes(5))
        {
            Status = "Stale";
            Process!.OutputDataReceived -= OutputReceived;
            Process!.CancelOutputRead();
        }
    }
}