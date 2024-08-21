using AbpDevTools.Notifications;
using AbpDevTools.Services;
using CliFx.Infrastructure;
using Spectre.Console;
using System.Net.Http.Json;

namespace AbpDevTools.Commands;

[Command("update", Description = "Checks for updates")]
public class UpdateCheckCommand : ICommand
{
    public bool Force { get; set; } = true;

    public bool Silent { get; set; }

    protected readonly UpdateChecker updateChecker;
    protected readonly INotificationManager notificationManager;

    public UpdateCheckCommand(UpdateChecker updateChecker, INotificationManager notificationManager)
    {
        this.updateChecker = updateChecker;
        this.notificationManager = notificationManager;
    }

    public ValueTask SoftCheckAsync(IConsole console)
    {
        Force = false;
        Silent = true;
        return ExecuteAsync(console);
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var command = "dotnet tool update -g AbpDevTools";

        if (Force)
        {
            console.Output.WriteLine($"Checking for updates...");
        }

        var result = await updateChecker.CheckAsync(force: Force);

        if (Force)
        {
            console.Output.WriteLine($"Current version: {result.CurrentVersion}");
        }

        if (result.UpdateAvailable)
        {
            await notificationManager.SendAsync(
                "AbpDevTools Update available!",
                $"Run '{command}' to update. A newer version {result.LatestVersion} available for AbpDevTools");

            AnsiConsole.Markup($"[yellow]A newer version {result.LatestVersion} available.[/]\n");

            AnsiConsole.Markup($"Run '[black on yellow]{command}[/]' to update.");
        }
        else
        {
            if (!Silent)
            {
                AnsiConsole.Markup($"[green]Your tool is up to date![/]\n");
            }
        }
    }
}
