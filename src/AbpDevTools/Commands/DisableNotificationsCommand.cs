using AbpDevTools.Configuration;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AbpDevTools.Commands;

[Command("disable-notifications")]
public class DisableNotificationsCommand : ICommand
{
    [CommandOption("uninstall", 'u', Description = "Uninstalls the 'BurntToast' powershell module.")]
    public bool UninstallBurntToast { get; set; }

    protected readonly NotificationConfiguration notificationConfiguration;

    public DisableNotificationsCommand(NotificationConfiguration notificationConfiguration)
    {
        this.notificationConfiguration = notificationConfiguration;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (UninstallBurntToast)
            {
                var process = Process.Start("powershell", "-Command Uninstall-Module -Name BurntToast");

                console.RegisterCancellationHandler().Register(() => process.Kill(entireProcessTree: true));

                await process.WaitForExitAsync();
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var options = notificationConfiguration.GetOptions();
            options.Enabled = false;
            notificationConfiguration.SetOptions(options);

            return;
        }

        throw new CommandException($"This operation isn't supported on {RuntimeInformation.OSDescription} currently. :(");
    }
}
