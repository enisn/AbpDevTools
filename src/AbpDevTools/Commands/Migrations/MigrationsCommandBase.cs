using AbpDevTools.Services;
using CliFx.Infrastructure;
using Spectre.Console;

namespace AbpDevTools.Commands.Migrations;
public abstract class MigrationsCommandBase : ICommand
{
    [CommandOption("all", 'a', Description = "Run the command for all the EF Core projects.")]
    public bool RunAll { get; set; }

    [CommandParameter(0, IsRequired = false, Description = "Working directory to run build. Probably project or solution directory path goes here. Default: . (Current Directory)")]
    public string? WorkingDirectory { get; set; }

    [CommandOption("projects", 'p', Description = "(Array) Names or part of names of projects will be ran.")]
    public string[] Projects { get; set; } = Array.Empty<string>();

    protected readonly EntityFrameworkCoreProjectsProvider entityFrameworkCoreProjectsProvider;

    protected MigrationsCommandBase(EntityFrameworkCoreProjectsProvider entityFrameworkCoreProjectsProvider)
    {
        this.entityFrameworkCoreProjectsProvider = entityFrameworkCoreProjectsProvider;
    }

    public virtual ValueTask ExecuteAsync(IConsole console)
    {
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }
        else
        {
            WorkingDirectory = Path.GetFullPath(WorkingDirectory);
        }

        return default;
    }

    protected async Task<FileInfo[]> ChooseProjectsAsync()
    {
        var projectFiles = await GetEfCoreProjectsAsync();

        if (projectFiles.Length == 0)
        {
           return Array.Empty<FileInfo>();
        }

        if (Projects.Length > 0)
        {
            projectFiles = projectFiles.Where(pf => Projects.Any(a => pf.FullName.Contains(a))).ToArray();
        }
        else if (!RunAll && projectFiles.Length > 1)
        {
            projectFiles = PromptForProjectSelection(projectFiles);
        }

        return projectFiles;
    }

    protected virtual FileInfo[] PromptForProjectSelection(FileInfo[] projectFiles)
    {
        var chosenProjects = AnsiConsole.Prompt(new MultiSelectionPrompt<FileInfo>()
                .Title("Choose project to create migrations.")
                .Required(true)
                .PageSize(12)
                .HighlightStyle(new Style(foreground: Color.MediumPurple2))
                .MoreChoicesText("[grey](Move up and down to reveal more projects)[/]")
                .InstructionsText(
                            "[grey](Press [mediumpurple2]<space>[/] to toggle a project, " +
                            "[green]<enter>[/] to accept)[/]")
                .UseConverter(GetProjectSelectionLabel)
                .AddChoices(projectFiles)
            );

        return chosenProjects.ToArray();
    }

    protected virtual string GetProjectSelectionLabel(FileInfo projectFile)
    {
        if (string.IsNullOrWhiteSpace(WorkingDirectory))
        {
            return projectFile.Name;
        }

        // Normalize defensively: callers may bypass ExecuteAsync (e.g. unit tests).
        var fullWorkingDirectory = Path.GetFullPath(WorkingDirectory);
        var relativePath = Path.GetRelativePath(fullWorkingDirectory, projectFile.FullName);
        return string.IsNullOrWhiteSpace(relativePath) || relativePath == "."
            ? projectFile.Name
            : relativePath;
    }

    protected virtual Task<FileInfo[]> GetEfCoreProjectsAsync()
    {
        // Pass Projects filter to provider for early filtering to improve performance
        return entityFrameworkCoreProjectsProvider.GetEfCoreProjectsAsync(WorkingDirectory!, Projects.Length > 0 ? Projects : null);
    }
}
