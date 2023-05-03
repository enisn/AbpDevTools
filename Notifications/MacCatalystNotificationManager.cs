using System.Diagnostics;

namespace AbpDevTools.Notifications;
public class MacCatalystNotificationManager : INotificationManager
{
    public async Task SendAsync(string title, string message = null, string icon = null)
    {
        var process = Process.Start("display", $"notification \"{message ?? string.Empty}\" with title \"{title}\"");

        await process.WaitForExitAsync();
    }
}
