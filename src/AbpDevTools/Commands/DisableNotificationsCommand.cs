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

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (UninstallBurntToast) 
            {
                var process = Process.Start("pwsh", "-Command Uninstall-Module -Name BurntToast");

                console.RegisterCancellationHandler().Register(() => process.Kill(entireProcessTree: true));

                await process.WaitForExitAsync();
            }

            var options = NotificationConfiguration.GetOptions();
            options.Enabled = false;
            NotificationConfiguration.SetOptions(options);

            return;
        }

        throw new CommandException($"This operation isn't supported on {RuntimeInformation.OSDescription} currently. :(");
    }
}
