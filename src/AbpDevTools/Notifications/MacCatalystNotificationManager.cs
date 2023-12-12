using System.Diagnostics;
using AbpDevTools.Configuration;

namespace AbpDevTools.Notifications;
public class MacCatalystNotificationManager : INotificationManager
{
    protected readonly ToolsConfiguration toolsConfiguration;
    protected readonly NotificationConfiguration notificationConfiguration;

    public MacCatalystNotificationManager(ToolsConfiguration toolsConfiguration, NotificationConfiguration notificationConfiguration)
    {
        this.toolsConfiguration = toolsConfiguration;
        this.notificationConfiguration = notificationConfiguration;
    }

    public async Task SendAsync(string title, string message = null, string icon = null)
    {
        if(!notificationConfiguration.GetOptions().Enabled){
            return;
        }

        var tools = toolsConfiguration.GetOptions();

        var process = Process.Start(tools["osascript"], $"-e \"display notification \\\"{message}\\\" with title \\\"{title}\\\"\"");

        await process.WaitForExitAsync();
    }
}
