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

            return;
        }

        throw new CommandException($"This operation isn't supported on {RuntimeInformation.OSDescription} currently. :(");
    }
}
