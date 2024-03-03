using System.Diagnostics;

namespace AbpDevTools.Commands;

public class RunningProjectItem
{
    public string? Name { get; set; }
    public Process? Process { get; set; }
    public virtual string? Status { get; set; }
    public virtual bool IsCompleted { get; set; }
    public virtual bool Queued { get; set; }

    public virtual void StartReadingOutput()
    {
    }
}

public class RunningCsProjItem : RunningProjectItem
{
    public RunningCsProjItem(string name, Process process, string? status = null)
    {
        this.Name = name;
        this.Process = process;
        this.Status = status ?? "Building...";
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
#if DEBUG
        if (!IsCompleted)
        {
            Status = args.Data?.Replace("[", string.Empty).Replace("]", string.Empty) ?? string.Empty;
        }
#endif

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

        if (args.Data != null && args.Data.Contains("** Angular Live Development Server is listening on "))
        {
            Status = args.Data
                [args.Data.IndexOf(", open your browser on ")..]
                .Replace(", open your browser on ", string.Empty)
                .Replace(" **", string.Empty);
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