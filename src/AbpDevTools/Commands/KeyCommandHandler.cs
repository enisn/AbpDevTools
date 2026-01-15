using System.Text;
using AbpDevTools.Services;
using Spectre.Console;
using CliFx.Infrastructure;

namespace AbpDevTools.Commands;

public class KeyCommandHandler
{
    private readonly List<RunningProjectItem> _runningProjects;
    private readonly IConsole _console;
    private readonly CancellationToken _cancellationToken;

    public KeyCommandMapping[] KeyCommandMappings { get; init; }

    public bool IsInnerCommandInProgress { get; set; }

    public KeyCommandHandler(List<RunningProjectItem> runningProjects, IConsole console, CancellationToken cancellationToken)
    {
        _runningProjects = runningProjects;
        _console = console;
        _cancellationToken = cancellationToken;
        KeyCommandMappings = new KeyCommandMapping[]
        {
            new KeyCommandMapping(new KeyPressEventArgs { Key = ConsoleKey.R}, HandleRestart, "Restart", "Restart all running applications"),
            new KeyCommandMapping(new KeyPressEventArgs { Key = ConsoleKey.R, CtrlPressed = true }, HandleRestartAll, "Restart All", "Restart a specific application"),
            new KeyCommandMapping(new KeyPressEventArgs { Key = ConsoleKey.S }, HandleStopOne, "Stop", "Stop a specific application"),
            new KeyCommandMapping(new KeyPressEventArgs { Key = ConsoleKey.L }, HandleLogs, "Logs", "View logs of a specific application"),
            new KeyCommandMapping(new KeyPressEventArgs { Key = ConsoleKey.H }, ShowHelp, "Help", "Show this help"),
        };
    }

    public Task<bool> HandleKeyPress(KeyPressEventArgs keyEvent)
    {
        var keyCommandMapping = KeyCommandMappings.FirstOrDefault(mapping => mapping.KeyPressEvent.Key == keyEvent.Key && mapping.KeyPressEvent.CtrlPressed == keyEvent.CtrlPressed && mapping.KeyPressEvent.ShiftPressed == keyEvent.ShiftPressed && mapping.KeyPressEvent.AltPressed == keyEvent.AltPressed);
        if (keyCommandMapping != null)
        {
            keyCommandMapping.Action();
        }
        return Task.FromResult(true);
    }

    public bool RequiresLiveRestart(KeyPressEventArgs keyEvent)
    {
        return keyEvent.Key == ConsoleKey.H || keyEvent.Key == ConsoleKey.R || keyEvent.Key == ConsoleKey.S || keyEvent.Key == ConsoleKey.L;
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

    private void HandleRestart()
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

    private void ShowHelp()
    {
        var helpTable = new Table()
            .Border(TableBorder.Rounded)
            .Title("[mediumpurple2]Available Commands[/]")
            .AddColumn("Key")
            .AddColumn("Action")
            .AddColumn("Description");

        foreach (var keyCommandMapping in KeyCommandMappings)
        {
            helpTable.AddRow($"[bold]{keyCommandMapping.GetKeyDisplay()}[/]", keyCommandMapping.Name, keyCommandMapping.Description ?? string.Empty);
        }

        helpTable.AddRow("[bold]Ctrl+C[/]", "Exit", "Send interrupt to stop all (exit)");
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

    private void HandleLogs()
    {
        if (_runningProjects.Count == 0)
        {
            _console.Output.WriteLine("\n[yellow]No projects to view logs.[/]");
            return;
        }

        var projectToView = _runningProjects.First();

        if (_runningProjects.Count > 1)
        {
            var projectChoices = _runningProjects.Select(p =>
            {
                var status = GetProjectStatus(p);
                return $"{p.Name}*[{status}]";
            }).ToList();

            projectChoices.Add("[red]Cancel[/]");

            var selectedProjectWithStatus = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Choose [mediumpurple2]project[/] to view logs:")
                    .HighlightStyle(new Style(foreground: Color.MediumPurple2))
                    .AddChoices(projectChoices));

            if (selectedProjectWithStatus == "[red]Cancel[/]")
            {
                _console.Output.WriteLine("\n[yellow]Operation cancelled.[/]");
                return;
            }

            var selectedProjectName = selectedProjectWithStatus.Split('*')[0];
            projectToView = _runningProjects.FirstOrDefault(p => p.Name == selectedProjectName);
            if (projectToView is null)
            {
                _console.Output.WriteLine($"[red]Project '{selectedProjectName}' not found.[/]");
                return;
            }
        }

        var previousLimit = projectToView.LogLimit;
        projectToView.LogLimit = -1;

        ShowLogsPanel(projectToView);

        projectToView.LogLimit = previousLimit;
        projectToView.TrimLogs(previousLimit);
    }

    private void ShowLogsPanel(RunningProjectItem project)
    {
        IsInnerCommandInProgress = true;
        AnsiConsole.Clear();

        var logs = project.GetLogs();
        var title = $"[mediumpurple2]Logs: {project.Name}[/]";

        AnsiConsole.MarkupLine(title);
        AnsiConsole.WriteLine();

        foreach (var log in logs)
        {
            AnsiConsole.MarkupLine(log);
        }

        project.LogAdded += LogAddedHandler;
        Console.Read();
        project.LogAdded -= LogAddedHandler;
        AnsiConsole.Clear();
        IsInnerCommandInProgress = false;
    }

    protected void LogAddedHandler(object? sender, string log)
    {
        AnsiConsole.MarkupLine(log);
    }
}
