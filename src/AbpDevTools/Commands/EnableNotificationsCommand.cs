using AbpDevTools.Configuration;
using AbpDevTools.Notifications;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AbpDevTools.Commands;

[Command("enable-notifications")]
public class EnableNotificationsCommand : ICommand
{
    protected INotificationManager notificationManager;

    public EnableNotificationsCommand(INotificationManager notificationManager)
    {
        this.notificationManager = notificationManager;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (!PowershellExistsInWindows())
            {
                throw new CommandException($"Powershell is not installed in your system. Please install it and try again.");
            }

            var process = Process.Start("powershell", "-Command Install-Module -Name BurntToast");

            console.RegisterCancellationHandler().Register(() => process.Kill(entireProcessTree: true));

            await process.WaitForExitAsync();

            var options = NotificationConfiguration.GetOptions();
            options.Enabled = true;
            NotificationConfiguration.SetOptions(options);

            await notificationManager.SendAsync("Notifications Enabled", "Notifications will be displayed like this.");

            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var options = NotificationConfiguration.GetOptions();
            options.Enabled = true;
            NotificationConfiguration.SetOptions(options);

            await notificationManager.SendAsync("Notifications Enabled", "Notifications will be displayed like this.");

            return;
        }

        throw new CommandException($"This operation isn't supported on {RuntimeInformation.OSDescription} currently. :(");
    }

    public bool PowershellExistsInWindows()
    {
        string regval = Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PowerShell\1", "Install", null).ToString();
        return regval.Equals("1");
    }
}
