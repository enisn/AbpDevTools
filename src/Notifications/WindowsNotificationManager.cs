using System.Diagnostics;

namespace AbpDevTools.Notifications;
public class WindowsNotificationManager : INotificationManager
{
    private string FolderPath => Path.Combine(
           Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
           "abpdev");

    public async Task SendAsync(string title, string message = null, string icon = null)
    {
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

        var process = Process.Start("pwsh", filePath);

        await process.WaitForExitAsync();

        File.Delete(filePath);
    }
}
