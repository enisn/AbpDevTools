using System.Diagnostics;
using System.Collections.Concurrent;
using Spectre.Console;

namespace AbpDevTools.Commands;

public class RunningProjectItem
{
    public string? Name { get; set; }
    public Process? Process { get; set; }
    public virtual string? Status { get; set; }
    public virtual bool IsCompleted { get; set; }
    public virtual bool Queued { get; set; }
    public bool Verbose { get; set; }
    public ProcessStartInfo? OriginalStartInfo { get; set; }
    public ConcurrentQueue<string> Logs { get; } = new();
    public int LogLimit { get; set; } = 200;

    public event EventHandler<string>? LogAdded;

    protected virtual void AddLog(string? log)
    {
        if (string.IsNullOrEmpty(log)) return;

        Logs.Enqueue(log);

        if (LogLimit > 0 && Logs.Count > LogLimit)
        {
            while (Logs.Count > LogLimit)
            {
                Logs.TryDequeue(out _);
            }
        }

        LogAdded?.Invoke(this, log);
    }

    public string[] GetLogs()
    {
        return Logs.ToArray();
    }

    public void TrimLogs(int limit)
    {
        if (limit > 0)
        {
            while (Logs.Count > limit)
            {
                Logs.TryDequeue(out _);
            }
        }
    }

    public virtual void StartReadingOutput()
    {
    }

    public virtual Process? Restart()
    {
        if (OriginalStartInfo == null)
            return null;

        try
        {
            Process = Process.Start(OriginalStartInfo);
            StartReadingOutput();
            return Process;
        }
        catch
        {
            return null;
        }
    }
}

public class RunningCsProjItem : RunningProjectItem
{
    public RunningCsProjItem(string name, Process process, ProcessStartInfo startInfo, string? status = null, bool verbose = false)
    {
        this.Name = name;
        this.Process = process;
        this.OriginalStartInfo = startInfo;
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
        Process!.ErrorDataReceived -= ErrorReceived;
        Process!.ErrorDataReceived += ErrorReceived;
        Process!.BeginErrorReadLine();
    }

    protected virtual void OutputReceived(object sender, DataReceivedEventArgs args)
    {
        if (args.Data == null)
        {
            return;
        }

        var log = Markup.Escape(args.Data);

        if (!IsCompleted && Verbose)
        {
            Status = log;
        }

        if (log.Contains("Now listening on: "))
        {
            Status = log[log.IndexOf("Now listening on: ")..];
            IsCompleted = true;
            AddLog($"[green]{log}[/]");
        }
        else if (log.Contains("dotnet watch ") && log.Contains(" Started"))
        {
            Status = log;
            IsCompleted = true;
            AddLog($"[green]{log}[/]");
        }
        else if (log.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                 log.Contains("fail", StringComparison.OrdinalIgnoreCase))
        {
            AddLog($"[red]{log}[/]");
        }
        else if (log.Contains("warn", StringComparison.OrdinalIgnoreCase))
        {
            AddLog($"[yellow]{log}[/]");
        }
        else
        {
            AddLog(log);
        }
    }

    private void ErrorReceived(object sender, DataReceivedEventArgs args)
    {
        if (args.Data == null) return;
        AddLog($"[red]{Markup.Escape(args.Data)}[/]");
    }
}

public class RunningInstallLibsItem : RunningProjectItem
{
    public RunningInstallLibsItem(string name, Process process, ProcessStartInfo startInfo, string? status = null)
    {
        this.Name = name;
        this.Process = process;
        this.OriginalStartInfo = startInfo;
        this.Status = status ?? "Installing...";
        StartReadingOutput();
    }

    public override void StartReadingOutput()
    {
        Process!.OutputDataReceived -= OutputReceived;
        Process!.OutputDataReceived += OutputReceived;
        Process!.BeginOutputReadLine();
        Process!.ErrorDataReceived -= ErrorReceived;
        Process!.ErrorDataReceived += ErrorReceived;
        Process!.BeginErrorReadLine();
    }

    protected virtual void OutputReceived(object sender, DataReceivedEventArgs args)
    {
        if (args.Data != null)
        {
            var log = Markup.Escape(args.Data);
            if (log.Contains("Done in"))
            {
                Status = "Completed.";
                IsCompleted = true;
                AddLog($"[green]{log}[/]");
            }
            else if (log.Contains("error", StringComparison.OrdinalIgnoreCase))
            {
                AddLog($"[red]{log}[/]");
            }
            else
            {
                AddLog(log);
            }
        }

        if (DateTime.Now - Process!.StartTime > TimeSpan.FromMinutes(5))
        {
            Status = "Stale";
        }
    }

    private void ErrorReceived(object sender, DataReceivedEventArgs args)
    {
        if (args.Data == null) return;
        AddLog($"[red]{Markup.Escape(args.Data)}[/]");
    }
}
