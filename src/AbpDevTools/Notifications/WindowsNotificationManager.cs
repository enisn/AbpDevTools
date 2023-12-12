using AbpDevTools.Configuration;
using System.Diagnostics;

namespace AbpDevTools.Notifications;
public class WindowsNotificationManager : INotificationManager
{
    private string FolderPath => Path.Combine(
           Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
           "abpdev");

    protected readonly ToolsConfiguration toolsConfiguration;
    protected readonly NotificationConfiguration notificationConfiguration;

    public WindowsNotificationManager(ToolsConfiguration toolsConfiguration, NotificationConfiguration notificationConfiguration)
    {
        this.toolsConfiguration = toolsConfiguration;
        this.notificationConfiguration = notificationConfiguration;
    }

    public async Task SendAsync(string title, string message = null, string icon = null)
    {
        if (!notificationConfiguration.GetOptions().Enabled)
        {
            return;
        }

        var command = "New-BurntToastNotification";

        command += $" -Text \"{title}\"";

        if (!string.IsNullOrEmpty(message))
        {
            command += $", \"{message}\"";
        }

        if (!string.IsNullOrEmpty(icon))
        {
            command += $" -AppLogo \"{icon}\"";
        }

        var fileName = Guid.NewGuid() + ".ps1";

        var filePath = Path.Combine(FolderPath, fileName);

        await File.WriteAllTextAsync(filePath, command);

        var tools = toolsConfiguration.GetOptions();
        var process = Process.Start(tools["powershell"], filePath);

        await process.WaitForExitAsync();

        File.Delete(filePath);
    }
}
