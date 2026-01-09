using System.Diagnostics;
using System.Runtime.InteropServices;
using AbpDevTools.Processes;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Spectre.Console;

namespace AbpDevTools.Commands;

[Command("find-port", Description = "Find and manage process using the specified port")]
public class FindPortCommand : ICommand
{
    [CommandParameter(0, IsRequired = true, Description = "Port number to find")]
    public int Port { get; set; }

    [CommandOption("kill", 'k', Description = "Kill the found process directly without asking")]
    public bool Kill { get; set; }

    protected readonly IProcessFinder processFinder;

    public FindPortCommand(IProcessFinder processFinder)
    {
        this.processFinder = processFinder;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var processes = await processFinder.FindProcessesByPortAsync(Port);

        if (!processes.Any())
        {
            AnsiConsole.MarkupLine($"[yellow]No process found using port [mediumpurple2]{Port}[/].[/]");
            return;
        }

        // If --kill flag is provided, kill directly without interactive menu
        if (Kill)
        {
            foreach (var process in processes)
            {
                KillSingleProcessDirectly(process);
            }
            return;
        }

        while (true)
        {
            AnsiConsole.Clear();

            // Display process list
            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("[grey]#[/]")
                .AddColumn("[grey]PID[/]")
                .AddColumn("[grey]Process Name[/]");

            foreach (var (process, index) in processes.Select((p, i) => (p, i)))
            {
                table.AddRow(
                    $"{index + 1}",
                    $"[mediumpurple2]{process.Pid}[/]",
                    $"[green]{process.ProcessName}[/]"
                );
            }

            AnsiConsole.Write(table);

            // Display actions
            AnsiConsole.WriteLine();
            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Choose an [mediumpurple2]action[/]?")
                    .PageSize(10)
                    .HighlightStyle(new Style(foreground: Color.MediumPurple2))
                    .MoreChoicesText("[grey](Move up and down to reveal more actions)[/]")
                    .AddChoices(new[] {
                        "Kill",
                        "Details",
                        "Open Location",
                        "Copy PID",
                        "Copy Path",
                        "Refresh",
                        "Exit"
                    }));

            switch (action)
            {
                case "Kill":
                    if (KillProcess(processes))
                    {
                        return;
                    }
                    break;
                case "Details":
                    ShowDetails(processes);
                    break;
                case "Open Location":
                    OpenLocation(processes);
                    break;
                case "Copy PID":
                    CopyToClipboard(processes.Select(p => p.Pid.ToString()).ToArray());
                    break;
                case "Copy Path":
                    CopyToClipboard(processes.Select(p => p.Path).ToArray());
                    break;
                case "Refresh":
                    processes = await processFinder.FindProcessesByPortAsync(Port);
                    if (!processes.Any())
                    {
                        AnsiConsole.MarkupLine($"[yellow]No process found using port [mediumpurple2]{Port}[/].[/]");
                        return;
                    }
                    continue;
                case "Exit":
                    return;
            }
        }
    }

    private bool KillProcess(List<ProcessInfo> processes)
    {
        if (processes.Count == 1)
        {
            return KillSingleProcess(processes[0]);
        }
        else
        {
            var target = AnsiConsole.Prompt(
                new SelectionPrompt<ProcessInfo>()
                    .Title("Select a [mediumpurple2]process[/] to kill")
                    .PageSize(10)
                    .HighlightStyle(new Style(foreground: Color.MediumPurple2))
                    .UseConverter(p => $"[red]{p.ProcessName}[/] (PID: {p.Pid})")
                    .AddChoices(processes));

            return KillSingleProcess(target);
        }
    }

    private void KillSingleProcessDirectly(ProcessInfo process)
    {
        try
        {
            var proc = System.Diagnostics.Process.GetProcessById(process.Pid);
            var startTime = proc.StartTime;
            var runningTime = DateTime.Now - startTime;
            var threadCount = proc.Threads.Count;

            proc.Kill(entireProcessTree: true);
            proc.WaitForExit();

            // Show details after killing
            AnsiConsole.MarkupLine($"[green]Process killed successfully![/]");

            var details = new Grid()
                .AddColumn()
                .AddColumn()
                .AddRow("[grey]PID:[/]", $"[mediumpurple2]{process.Pid}[/]")
                .AddRow("[grey]Process Name:[/]", $"[green]{process.ProcessName}[/]")
                .AddRow("[grey]Path:[/]", $"[blue]{process.Path}[/]")
                .AddRow("[grey]Start Time:[/]", $"[yellow]{startTime:yyyy-MM-dd HH:mm:ss}[/]")
                .AddRow("[grey]Running Time:[/]", $"[yellow]{runningTime:hh\\:mm\\:ss}[/]")
                .AddRow("[grey]Threads:[/]", $"[mediumpurple2]{threadCount}[/]");

            AnsiConsole.Write(details);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to kill process [red]{process.ProcessName}[/] (PID: {process.Pid}): {ex.Message}[/]");
        }
    }

    private bool KillSingleProcess(ProcessInfo process)
    {
        if (!AnsiConsole.Confirm($"Are you sure you want to kill [red]{process.ProcessName}[/] (PID: {process.Pid})?"))
        {
            return false;
        }

        try
        {
            var proc = System.Diagnostics.Process.GetProcessById(process.Pid);
            proc.Kill(entireProcessTree: true);
            proc.WaitForExit();

            AnsiConsole.MarkupLine($"[green]Process [red]{process.ProcessName}[/] (PID: {process.Pid}) has been killed.[/]");
            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to kill process: {ex.Message}[/]");
            AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
            Console.ReadKey();
            return false;
        }
    }

    private void ShowDetails(List<ProcessInfo> processes)
    {
        if (processes.Count == 1)
        {
            ShowSingleProcessDetails(processes[0]);
        }
        else
        {
            var target = AnsiConsole.Prompt(
                new SelectionPrompt<ProcessInfo>()
                    .Title("Select a [mediumpurple2]process[/] to view details")
                    .PageSize(10)
                    .HighlightStyle(new Style(foreground: Color.MediumPurple2))
                    .UseConverter(p => $"{p.ProcessName} (PID: {p.Pid})")
                    .AddChoices(processes));

            ShowSingleProcessDetails(target);
        }
    }

    private void ShowSingleProcessDetails(ProcessInfo process)
    {
        try
        {
            var proc = System.Diagnostics.Process.GetProcessById(process.Pid);

            var details = new Grid()
                .AddColumn()
                .AddColumn()
                .AddRow("[grey]PID:[/]", $"[mediumpurple2]{process.Pid}[/]")
                .AddRow("[grey]Process Name:[/]", $"[green]{process.ProcessName}[/]")
                .AddRow("[grey]Path:[/]", $"[blue]{process.Path}[/]")
                .AddRow("[grey]Start Time:[/]", $"[yellow]{proc.StartTime:yyyy-MM-dd HH:mm:ss}[/]")
                .AddRow("[grey]Running Time:[/]", $"[yellow]{(DateTime.Now - proc.StartTime):hh\\:mm\\:ss}[/]")
                .AddRow("[grey]Threads:[/]", $"[mediumpurple2]{proc.Threads.Count}[/]");

            AnsiConsole.Write(details);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to get process details: {ex.Message}[/]");
        }

        AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
        Console.ReadKey();
    }

    private void OpenLocation(List<ProcessInfo> processes)
    {
        if (processes.Count == 1)
        {
            OpenSingleLocation(processes[0]);
        }
        else
        {
            var target = AnsiConsole.Prompt(
                new SelectionPrompt<ProcessInfo>()
                    .Title("Select a [mediumpurple2]process[/] to open location")
                    .PageSize(10)
                    .HighlightStyle(new Style(foreground: Color.MediumPurple2))
                    .UseConverter(p => $"{p.ProcessName} (PID: {p.Pid})")
                    .AddChoices(processes));

            OpenSingleLocation(target);
        }
    }

    private void OpenSingleLocation(ProcessInfo process)
    {
        try
        {
            var directory = Path.GetDirectoryName(process.Path);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                AnsiConsole.MarkupLine($"[yellow]Cannot find location for process: {process.Path}[/]");
                AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
                Console.ReadKey();
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo("explorer", directory) { UseShellExecute = true });
            }
            else
            {
                Process.Start(new ProcessStartInfo("open", directory) { UseShellExecute = true });
            }

            AnsiConsole.MarkupLine($"[green]Opening location for [mediumpurple2]{process.ProcessName}[/]...[/]");
            AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
            Console.ReadKey();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to open location: {ex.Message}[/]");
            AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
            Console.ReadKey();
        }
    }

    private void CopyToClipboard(string[] values)
    {
        if (values.Length == 0) return;

        var text = values.Length == 1 ? values[0] : string.Join(Environment.NewLine, values);

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var PowerShellScript = $"Set-Clipboard -Value \"{text.Replace("\"", "`\"")}\"";
                Process.Start("PowerShell", $"-NoProfile -Command \"{PowerShellScript}\"")?.WaitForExit();
            }
            else
            {
                // For Unix-like systems, try using xclip or xsel
                try
                {
                    var tempFile = Path.GetTempFileName();
                    File.WriteAllText(tempFile, text);
                    Process.Start("sh", $"-c \"cat {tempFile} | xclip -selection clipboard 2>/dev/null || cat {tempFile} | pbcopy 2>/dev/null\"")?.WaitForExit();
                    File.Delete(tempFile);
                }
                catch
                {
                    throw new PlatformNotSupportedException("Clipboard requires xclip (Linux) or pbcopy (macOS)");
                }
            }

            var item = values.Length == 1 ? "item" : "items";
            AnsiConsole.MarkupLine($"[green]Copied {values.Length} {item} to clipboard.[/]");
            AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
            Console.ReadKey();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to copy to clipboard: {ex.Message}[/]");
            AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
            Console.ReadKey();
        }
    }
}
