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
                HandleStopOne();
                return Task.FromResult(true);

            case ConsoleKey.H:
                ShowHelp();
                return Task.FromResult(true);

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
        
        // Add cancel option
        projectChoices.Add("[red]Cancel[/]");
        
        var selectedProjectWithStatus = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Choose [mediumpurple2]project[/] to restart:")
                .HighlightStyle(new Style(foreground: Color.MediumPurple2))
                .AddChoices(projectChoices));

        // Check if user selected cancel
        if (selectedProjectWithStatus == "[red]Cancel[/]")
        {
            _console.Output.WriteLine("\n[yellow]Operation cancelled.[/]");
            return;
        }

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
        helpTable.AddRow("[bold]S[/]", "Stop Specific", "Stop a specific application");
        helpTable.AddRow("[bold]Ctrl+C[/]", "Exit", "Send interrupt to stop all (exit)");
        helpTable.AddRow("[bold]H[/]", "Help", "Show this help");

        AnsiConsole.Write(helpTable);
        _console.Output.WriteLine("\nPress any key to continue...");
        Console.ReadKey(true);
    }

    private void HandleStopOne()
    {
        if (_runningProjects.Count == 0)
        {
            _console.Output.WriteLine("\n[yellow]No projects to stop.[/]");
            return;
        }

        var projectChoices = _runningProjects.Select(p =>
        {
            var status = GetProjectStatus(p);
            return $"{p.Name} [{status}]";
        }).ToList();

        projectChoices.Add("[red]Cancel[/]");

        var selectedProjectWithStatus = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Choose [mediumpurple2]project[/] to stop:")
                .HighlightStyle(new Style(foreground: Color.MediumPurple2))
                .AddChoices(projectChoices));

        if (selectedProjectWithStatus == "[red]Cancel[/]")
        {
            _console.Output.WriteLine("\n[yellow]Operation cancelled.[/]");
            return;
        }

        var selectedProjectName = selectedProjectWithStatus.Split(' ')[0];
        var projectToStop = _runningProjects.First(p => p.Name == selectedProjectName);

        _console.Output.WriteLine($"\n[yellow]Stopping {selectedProjectName}...[/]");

        if (projectToStop.Process?.HasExited == false)
        {
            try
            {
                projectToStop.Process.Kill(entireProcessTree: true);
                projectToStop.Process.WaitForExit();
                projectToStop.Status = "[red]*[/] Stopped";
                projectToStop.IsCompleted = true;
            }
            catch (Exception ex)
            {
                _console.Output.WriteLine($"[red]Failed to stop {selectedProjectName}:[/] {Markup.Escape(ex.Message)}");
                return;
            }
        }

        _console.Output.WriteLine($"[green]{selectedProjectName} stopped![/]");
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
