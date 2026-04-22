using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AbpDevTools;

[RegisterTransient]
public class Platform
{
    public virtual void Open(string filePath)
    {
        OpenProcess(filePath).WaitForExit();
    }

    public virtual Task OpenAsync(string filePath)
    {
        return OpenProcess(filePath).WaitForExitAsync();
    }

    private Process OpenProcess(string filePath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Process.Start(new ProcessStartInfo("explorer", filePath));
        }
        else
        {
            return Process.Start(new ProcessStartInfo("open", filePath));
        }
    }
}
