using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AbpDevTools.Processes;

public class UnixProcessFinder : IProcessFinder
{
    public async Task<List<ProcessInfo>> FindProcessesByPortAsync(int port)
    {
        var processes = new List<ProcessInfo>();

        try
        {
            // Use lsof to find processes using the specified port
            var arguments = $"-i :{port} -P -n";

            var startInfo = new ProcessStartInfo("lsof", arguments)
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

            // Skip header line
            foreach (var line in lines.Skip(1))
            {
                // lsof output format: COMMAND PID USER FD TYPE DEVICE SIZE/OFF NODE NAME
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && int.TryParse(parts[1], out var pid))
                {
                    var processInfo = GetProcessInfo(pid);
                    if (processInfo != null)
                    {
                        processes.Add(processInfo);
                    }
                }
            }
        }
        catch
        {
            // lsof might not be installed or failed - silently handle
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
