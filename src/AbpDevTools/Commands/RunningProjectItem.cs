using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AbpDevTools.Commands;

public class RunningProjectItem
{
    public string? Name { get; set; }
    public Process? Process { get; set; }
    public virtual string? Status { get; set; }
    public virtual bool IsCompleted { get; set; }
    public virtual bool Queued { get; set; }
    public string? LaunchUrl { get; protected set; }

    public virtual void StartReadingOutput()
    {
    }
}

public class RunningCsProjItem : RunningProjectItem
{
    protected Action<string>? LaunchAction { get; set; }

    public RunningCsProjItem(string name, Process process, string? status = null, Action<string>? launchAction = null)
    {
        this.Name = name;
        this.Process = process;
        this.Status = status ?? "Building...";
        StartReadingOutput();
        LaunchAction = launchAction;
    }

    public override void StartReadingOutput()
    {
        Queued = false;
        Status = "Waiting output...";
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
            TryLaunch();
        }

        if (args.Data != null && 
            args.Data.Contains("dotnet watch ") &&
            args.Data.Contains(" Started"))
        {
            Status = args.Data;
            Process?.CancelOutputRead();
            IsCompleted = true;
            TryLaunch();
        }

        if (DateTime.Now - Process?.StartTime > TimeSpan.FromMinutes(5))
        {
            Status = "Stale";
            Process!.OutputDataReceived -= OutputReceived;
            Process.CancelOutputRead();
        }
    }

    protected virtual void TryLaunch()
    {
        var _launchUrl = Status?.Split(" ").Last().Trim();
        if (Uri.TryCreate(_launchUrl, UriKind.RelativeOrAbsolute, out _))
        {
            LaunchUrl = _launchUrl;
        }
        else
        {
            var launchSettingsPath = Path.Combine(Process!.StartInfo.WorkingDirectory, "Properties", "launchSettings.json");

            if (File.Exists(launchSettingsPath))
            {
                var launchSettings = JsonNode.Parse(File.ReadAllText(launchSettingsPath));
                var profiles = launchSettings!["profiles"];
                foreach (var profile in profiles!.AsObject())
                {
                    var applicationUrl = profile.Value!["applicationUrl"]?.ToString();
                    if (!string.IsNullOrEmpty(applicationUrl))
                    {
                        LaunchUrl = applicationUrl;
                        break;
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(LaunchUrl))
        {
            LaunchAction?.Invoke(LaunchUrl);
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