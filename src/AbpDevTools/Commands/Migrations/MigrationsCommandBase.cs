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
        else if (!RunAll && projectFiles.Length > 0)
        {
            var chosenProjects = AnsiConsole.Prompt(new MultiSelectionPrompt<string>()
                .Title("Choose project to create migrations.")
                .Required(true)
                .PageSize(12)
                .HighlightStyle(new Style(foreground: Color.MediumPurple2))
                .MoreChoicesText("[grey](Move up and down to reveal more projects)[/]")
                .InstructionsText(
                            "[grey](Press [mediumpurple2]<space>[/] to toggle a project, " +
                            "[green]<enter>[/] to accept)[/]")
                .AddChoices(projectFiles
                    .Select(p => Path.GetDirectoryName(p.FullName.Replace(WorkingDirectory, string.Empty)).Trim('\\'))
                    .ToArray())
            );

            projectFiles = projectFiles.Where(p => chosenProjects.Any(cp => p.FullName.Contains(cp))).ToArray();
        }

        return projectFiles;
    }

    async Task<FileInfo[]> GetEfCoreProjectsAsync()
    {
        return await entityFrameworkCoreProjectsProvider.GetEfCoreProjectsAsync(WorkingDirectory!);
    }
}
