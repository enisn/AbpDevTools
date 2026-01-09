using System.Diagnostics;
using System.Text;

namespace AbpDevTools.Processes;

public class WindowsProcessFinder : IProcessFinder
{
    public async Task<List<ProcessInfo>> FindProcessesByPortAsync(int port)
    {
        var processes = new List<ProcessInfo>();

        try
        {
            // Use netstat to find connections on the specified port
            var startInfo = new ProcessStartInfo("netstat", "-ano")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return processes;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            process.WaitForExit();

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                // Netstat output format: Proto Local Address Foreign Address State PID
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 5)
                {
                    var localAddress = parts[1];
                    var pid = parts[parts.Length - 1];

                    // Parse local address (e.g., 0.0.0.0:3000 or [::]:3000)
                    if (localAddress.Contains(':') && localAddress.Contains($":{port}"))
                    {
                        if (int.TryParse(pid, out var processId))
                        {
                            var processInfo = GetProcessInfo(processId);
                            if (processInfo != null)
                            {
                                processes.Add(processInfo);
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Silently handle errors
        }

        return processes.DistinctBy(p => p.Pid).ToList();
    }

    private ProcessInfo? GetProcessInfo(int pid)
    {
        try
        {
            var process = System.Diagnostics.Process.GetProcessById(pid);
            return new ProcessInfo
            {
                Pid = pid,
                ProcessName = process.ProcessName,
                Path = TryGetProcessPath(process) ?? "Unknown"
            };
        }
        catch
        {
            // Process might have ended or access denied
            return null;
        }
    }

    private string? TryGetProcessPath(System.Diagnostics.Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            // Some system processes don't allow access to MainModule
            return null;
        }
    }
}
