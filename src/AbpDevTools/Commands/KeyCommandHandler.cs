using AbpDevTools.Services;
using Spectre.Console;
using CliFx.Infrastructure;

namespace AbpDevTools.Commands;

public class KeyCommandHandler
{
    private readonly List<RunningProjectItem> _runningProjects;
    private readonly IConsole _console;
    private readonly CancellationToken _cancellationToken;

    public KeyCommandHandler(List<RunningProjectItem> runningProjects, IConsole console, CancellationToken cancellationToken)
    {
        _runningProjects = runningProjects;
        _console = console;
        _cancellationToken = cancellationToken;
    }

    public Task<bool> HandleKeyPress(KeyPressEventArgs keyEvent)
    {
        switch (keyEvent.Key)
        {
            case ConsoleKey.R:
                if (keyEvent.CtrlPressed)
                {
                    HandleCtrlR();
                }
                else
                {
                    HandleRestartAll();
                }
                return Task.FromResult(true);

            case ConsoleKey.S:
                HandleStopAll();
                return Task.FromResult(false);

            case ConsoleKey.K:
                HandleKillAll();
                return Task.FromResult(false);

            case ConsoleKey.H:
                ShowHelp();
                return Task.FromResult(true);

            case ConsoleKey.Q:
                return Task.FromResult(false); // Signal to quit

            default:
                return Task.FromResult(true); // Continue listening
        }
    }

    private void HandleRestartAll()
    {
        _console.Output.WriteLine("\n[orange1]Restarting all applications...[/]");
        
        foreach (var project in _runningProjects)
        {
            if (project.Process?.HasExited == false)
            {
                project.Process.Kill(entireProcessTree: true);
                project.Process.WaitForExit();
            }
            
            RestartProject(project);
        }
        
        _console.Output.WriteLine("[green]All applications restarted![/]");
    }

    private void HandleCtrlR()
    {
        if (_runningProjects.Count == 0)
        {
            _console.Output.WriteLine("\n[yellow]No projects to restart.[/]");
            return;
        }

        // Show all projects with their status
        var projectChoices = _runningProjects.Select(p => 
        {
            var status = GetProjectStatus(p);
            return $"{p.Name} [{status}]";
        }).ToList();
        
        var selectedProjectWithStatus = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Choose [mediumpurple2]project[/] to restart:")
                .HighlightStyle(new Style(foreground: Color.MediumPurple2))
                .AddChoices(projectChoices));

        // Extract project name (remove status part)
        var selectedProjectName = selectedProjectWithStatus.Split(' ')[0];
        var projectToRestart = _runningProjects.First(p => p.Name == selectedProjectName);
        
        _console.Output.WriteLine($"\n[orange1]Restarting {selectedProjectName}...[/]");
        
        // Kill process if it's still running
        if (projectToRestart.Process?.HasExited == false)
        {
            projectToRestart.Process.Kill(entireProcessTree: true);
            projectToRestart.Process.WaitForExit();
        }
        
        RestartProject(projectToRestart);
        _console.Output.WriteLine($"[green]{selectedProjectName} restarted![/]");
    }

    private string GetProjectStatus(RunningProjectItem project)
    {
        if (project.Process == null)
            return "red]Not Started[/";
        
        if (project.Process.HasExited)
            return $"red]Exited({project.Process.ExitCode})[/";
        
        if (project.IsCompleted)
            return "green]Running[/";
        
        return "yellow]Starting[/";
    }

    private void HandleStopAll()
    {
        _console.Output.WriteLine("\n[yellow]Stopping all applications gracefully...[/]");
        
        foreach (var project in _runningProjects)
        {
            if (project.Process?.HasExited == false)
            {
                project.Process.Kill(entireProcessTree: true);
                project.Process.WaitForExit();
            }
        }
        
        _console.Output.WriteLine("[green]All applications stopped![/]");
    }

    private void HandleKillAll()
    {
        _console.Output.WriteLine("\n[red]Force killing all applications...[/]");
        
        foreach (var project in _runningProjects)
        {
            if (project.Process?.HasExited == false)
            {
                project.Process.Kill(entireProcessTree: true);
                project.Process.WaitForExit();
            }
        }
        
        _console.Output.WriteLine("[green]All applications killed![/]");
    }

    private void ShowHelp()
    {
        var helpTable = new Table()
            .Border(TableBorder.Rounded)
            .Title("[mediumpurple2]Available Commands[/]")
            .AddColumn("Key")
            .AddColumn("Action")
            .AddColumn("Description");

        helpTable.AddRow("[bold]R[/]", "Restart All", "Restart all running applications");
        helpTable.AddRow("[bold]Ctrl+R[/]", "Restart Specific", "Restart a specific application");
        helpTable.AddRow("[bold]S[/]", "Stop All", "Stop all applications gracefully");
        helpTable.AddRow("[bold]K[/]", "Kill All", "Force kill all applications");
        helpTable.AddRow("[bold]H[/]", "Help", "Show this help");
        helpTable.AddRow("[bold]Q[/]", "Quit", "Exit the application");

        AnsiConsole.Write(helpTable);
        _console.Output.WriteLine("\nPress any key to continue...");
        Console.ReadKey(true);
    }

    private void RestartProject(RunningProjectItem project)
    {
        project.Status = "[orange1]*[/] Restarting...";
        project.IsCompleted = false;
        project.Queued = false;
        
        var newProcess = project.Restart();
        if (newProcess != null)
        {
            project.Status = "Building...";
        }
        else
        {
            project.Status = "[red]*[/] Failed to restart";
        }
    }
}
