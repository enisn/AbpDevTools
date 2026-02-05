using AbpDevTools.Notifications;
using AbpDevTools.Services;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Spectre.Console;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AbpDevTools.Commands;

[Command("update", Description = "Checks for updates")]
public class UpdateCheckCommand : ICommand
{
    [CommandOption("apply", Description = "Apply the update if available")]
    public bool Apply { get; set; }

    [CommandOption("yes", 'y', Description = "Skip confirmation prompt")]
    public bool Yes { get; set; }

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
        if (Apply)
        {
            await PerformSelfUpdateAsync(console);
            return;
        }

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
            var selfUpdateCommand = "abpdev update --apply";

            await notificationManager.SendAsync(
                "AbpDevTools Update available!",
                $"Run '{selfUpdateCommand}' to update. A newer version {result.LatestVersion} available for AbpDevTools");

            AnsiConsole.Markup($"[yellow]A newer version {result.LatestVersion} available.[/]\n");

            AnsiConsole.Markup($"\n[blue]To update now, run:[/]\n");
            AnsiConsole.Markup($"  [black on green] {selfUpdateCommand} [/]\n");
            AnsiConsole.Markup($"\n[grey]Or manually:[/] [yellow]{command}[/]");
        }
        else
        {
            if (!Silent)
            {
                AnsiConsole.Markup($"[green]Your tool is up to date![/]\n");
            }
        }
    }

    private async ValueTask PerformSelfUpdateAsync(IConsole console)
    {
        AnsiConsole.Markup("[grey]Checking for updates...[/]\n");

        var result = await updateChecker.CheckAsync(force: true);
        AnsiConsole.Markup($"Current version: {result.CurrentVersion}\n");

        if (!result.UpdateAvailable)
        {
            AnsiConsole.Markup("[green]Your tool is already up to date![/]\n");
            return;
        }

        AnsiConsole.Markup($"[yellow]A newer version {result.LatestVersion} is available.[/]\n");

        if (!Yes)
        {
            var confirm = AnsiConsole.Prompt(
                new ConfirmationPrompt("Do you want to update now?")
                {
                    DefaultValue = true
                });

            if (!confirm)
            {
                AnsiConsole.Markup("[grey]Update cancelled.[/]\n");
                return;
            }
        }

        AnsiConsole.Markup("[blue]Starting self-update process...[/]\n");

        try
        {
            await SpawnUpdateProcessAsync();
            
            AnsiConsole.Markup("[green]Update process started successfully![/]\n");
            AnsiConsole.Markup("[grey]The update is running in the background. This process will now exit.[/]\n");
            AnsiConsole.Markup("[grey]Please wait a moment, then run 'abpdev --version' to verify the update.[/]\n");
            
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            AnsiConsole.Markup($"[red]Failed to start update process: {ex.Message}[/]\n");
            AnsiConsole.Markup($"[yellow]You can manually update by running: dotnet tool update -g AbpDevTools[/]\n");
            Environment.Exit(1);
        }
    }

    private async Task SpawnUpdateProcessAsync()
    {
        var startInfo = GetUpdateProcessStartInfo();
        
         var process = Process.Start(startInfo);

         if (process is null)
         {
            throw new InvalidOperationException("Failed to start the update process.");
         }

        await Task.CompletedTask;
    }

    private ProcessStartInfo GetUpdateProcessStartInfo()
    {
        return new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"tool update -g AbpDevTools",
            UseShellExecute = true,
            CreateNoWindow = false,
            WindowStyle = ProcessWindowStyle.Normal,
        };
    }
}
