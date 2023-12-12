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
    protected ToolsConfiguration toolsConfiguration;
    protected NotificationConfiguration notificationConfiguration;

    public EnableNotificationsCommand(INotificationManager notificationManager, ToolsConfiguration toolsConfiguration, NotificationConfiguration notificationConfiguration)
    {
        this.notificationManager = notificationManager;
        this.toolsConfiguration = toolsConfiguration;
        this.notificationConfiguration = notificationConfiguration;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (!PowerShellExistsInWindows())
            {
                throw new CommandException($"PowerShell is not installed in your system. Please install it and try again.");
            }

            var tools = toolsConfiguration.GetOptions();
            var process = Process.Start(tools["powershell"], "-Command Install-Module -Name BurntToast");

            console.RegisterCancellationHandler().Register(() => process.Kill(entireProcessTree: true));

            await process.WaitForExitAsync();

            var options = notificationConfiguration.GetOptions();
            options.Enabled = true;
            notificationConfiguration.SetOptions(options);

            await notificationManager.SendAsync("Notifications Enabled", "Notifications will be displayed like this.");

            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var options = notificationConfiguration.GetOptions();
            options.Enabled = true;
            notificationConfiguration.SetOptions(options);

            await notificationManager.SendAsync("Notifications Enabled", "Notifications will be displayed like this.");

            return;
        }

        throw new CommandException($"This operation isn't supported on {RuntimeInformation.OSDescription} currently. :(");
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    public bool PowerShellExistsInWindows()
    {
        string regval = Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PowerShell\1", "Install", null).ToString();
        return regval.Equals("1");
    }
}
