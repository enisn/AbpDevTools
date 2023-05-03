using AbpDevTools.Configuration;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AbpDevTools.Commands;

[Command("enable-notifications")]
public class EnableNotificationsCommand : ICommand
{
    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var process = Process.Start("pwsh", "-Command Install-Module -Name BurntToast");

            console.RegisterCancellationHandler().Register(() => process.Kill(entireProcessTree: true));

            await process.WaitForExitAsync();

            var options = NotificationConfiguration.GetOptions();
            options.Enabled = true;
            NotificationConfiguration.SetOptions(options);

            return;
        }

        if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var process = Process.Start("bash", "osascript -e 'display notification \"Notifications enabled.\" with title \"AbpDevTools\"'");

            console.RegisterCancellationHandler().Register(() => process.Kill(entireProcessTree: true));

            await process.WaitForExitAsync();

            var options = NotificationConfiguration.GetOptions();
            options.Enabled = true;
            NotificationConfiguration.SetOptions(options);

            return;
        }

        throw new CommandException($"This operation isn't supported on {RuntimeInformation.OSDescription} currently. :(");
    }
}
