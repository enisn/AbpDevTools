using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AbpDevTools;
public static class Platform
{
    public static void Open(string filePath)
    {
        OpenProcess(filePath).WaitForExit();
    }

    public static Task OpenAsync(string filePath)
    {
        return OpenProcess(filePath).WaitForExitAsync();
    }

    private static Process OpenProcess(string filePath)
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
