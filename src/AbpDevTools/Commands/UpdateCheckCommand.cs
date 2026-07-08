using AbpDevTools.Configuration;
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
    protected readonly ToolsConfiguration toolsConfiguration;

    public UpdateCheckCommand(
        UpdateChecker updateChecker,
        INotificationManager notificationManager,
        ToolsConfiguration toolsConfiguration)
    {
        this.updateChecker = updateChecker;
        this.notificationManager = notificationManager;
        this.toolsConfiguration = toolsConfiguration;
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
            var tools = toolsConfiguration.GetOptions();
            var command = FormatManualUpdateCommand(tools["dotnet"]);
            var selfUpdateCommand = "abpdev update --apply";

            await notificationManager.SendAsync(
                "AbpDevTools Update available!",
                $"Run '{selfUpdateCommand}' to update. A newer version {result.LatestVersion} available for AbpDevTools");

            AnsiConsole.Markup($"[yellow]A newer version {result.LatestVersion} available.[/]\n");

            AnsiConsole.Markup($"\n[blue]To update now, run:[/]\n");
            AnsiConsole.Markup($"  [black on green] {selfUpdateCommand} [/]\n");
            AnsiConsole.Markup($"\n[grey]Or manually:[/] [yellow]{Markup.Escape(command)}[/]");
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
            var confirm = global::AbpDevTools.ConsoleSupport.ConfirmOrDefault(
                console,
                "Do you want to update now?",
                defaultValue: false,
                fallbackMessage: "Interactive confirmation is unavailable; update cancelled. Pass '--yes' to apply the update non-interactively.");

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
            var tools = toolsConfiguration.GetOptions();
            AnsiConsole.Markup($"[yellow]You can manually update by running: {Markup.Escape(FormatManualUpdateCommand(tools["dotnet"]))}[/]\n");
            Environment.Exit(1);
        }
    }

    private async Task SpawnUpdateProcessAsync()
    {
        var tools = toolsConfiguration.GetOptions();
        var startInfo = CreateUpdateProcessStartInfo(Environment.ProcessId, tools["powershell"], tools["dotnet"]);
        
        var process = Process.Start(startInfo);

        if (process is null)
        {
            throw new InvalidOperationException("Failed to start the update process.");
        }

        await Task.CompletedTask;
    }

    internal static ProcessStartInfo CreateUpdateProcessStartInfo(int currentProcessId, string powershell, string dotnet)
    {
        var dotnetPath = ResolveExecutablePath(dotnet);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ResolveExecutablePath(powershell),
                UseShellExecute = false,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal,
            };

            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add($"Wait-Process -Id {currentProcessId} -ErrorAction SilentlyContinue; & {QuotePowerShellLiteral(dotnetPath)} tool update -g AbpDevTools; exit $LASTEXITCODE");

            return startInfo;
        }

        var shellStartInfo = new ProcessStartInfo
        {
            FileName = "/bin/sh",
            UseShellExecute = false,
            CreateNoWindow = false,
            WindowStyle = ProcessWindowStyle.Normal,
        };

        shellStartInfo.ArgumentList.Add("-c");
        shellStartInfo.ArgumentList.Add($"while kill -0 {currentProcessId} 2>/dev/null; do sleep 1; done; {QuoteShellLiteral(dotnetPath)} tool update -g AbpDevTools");

        return shellStartInfo;
    }

    private static string ResolveExecutablePath(string executable)
    {
        if (Path.IsPathRooted(executable) || executable.Contains(Path.DirectorySeparatorChar) || executable.Contains(Path.AltDirectorySeparatorChar))
        {
            return executable;
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return executable;
        }

        var extensions = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            : [string.Empty];

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory, executable.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? executable : executable + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return executable;
    }

    private static string FormatManualUpdateCommand(string dotnet)
    {
        return $"{QuoteCommandForDisplay(dotnet)} tool update -g AbpDevTools";
    }

    private static string QuoteCommandForDisplay(string value)
    {
        return value.Contains(' ') ? $"\"{value}\"" : value;
    }

    private static string QuotePowerShellLiteral(string value)
    {
        return $"'{value.Replace("'", "''")}'";
    }

    private static string QuoteShellLiteral(string value)
    {
        return $"'{value.Replace("'", "'\\''")}'";
    }
}
