using AbpDevTools.Configuration;
using CliFx.Infrastructure;
using Spectre.Console;
using YamlDotNet.Serialization;

namespace AbpDevTools.Running;

[Command("generate run-yml", Description = "Generates running configuration file.")]
public class GenerateRunningConfigurationCommand : ICommand
{
    [CommandParameter(0, IsRequired = false, Description = "Working directory to run build. Probably project or solution directory path goes here. Default: . (Current Directory)")]
    public string WorkingDirectory { get; set; }

    [CommandOption("name", 'n', Description = "Name of the file. ({name}.yml)")]
    public string Name { get; set; } = ".abpdevrun";

    [CommandOption("projects", 'p', Description = "(Array) Names or part of names of projects will be ran.")]
    public string[] Projects { get; set; }

    [CommandOption("configuration", 'c')]
    public string Configuration { get; set; }

    [CommandOption("env", 'e', Description = "Uses the virtual environment for this process. Use 'abpdev env config' command to see/manage environments.")]
    public string EnvironmentName { get; set; }

    protected IConsole console;

    protected ISerializer yamlSerializer;

    public GenerateRunningConfigurationCommand(ISerializer yamlSerializer)
    {
        this.yamlSerializer = yamlSerializer;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        this.console = console;
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }

        var cancellationToken = console.RegisterCancellationHandler();

        var _runnableProjects = RunConfiguration.GetOptions().RunnableProjects;

        FileInfo[] csprojs = await AnsiConsole.Status()
            .StartAsync("Looking for projects", async ctx =>
            {
                ctx.Spinner(Spinner.Known.SimpleDotsScrolling);
                return Directory.EnumerateFiles(WorkingDirectory, "*.csproj", SearchOption.AllDirectories)
                    .Where(x => _runnableProjects.Any(y => x.EndsWith(y + ".csproj")))
                    .Select(x => new FileInfo(x))
                    .ToArray();
            });

        await console.Output.WriteLineAsync($"{csprojs.Length} csproj file(s) found.");
        var projects = csprojs.Where(x => !x.Name.Contains(".DbMigrator")).ToArray();

        if (projects.Length > 1)
        {
            await console.Output.WriteLineAsync($"\n");

            if (Projects == null || Projects.Length == 0)
            {
                var choosedProjects = AnsiConsole.Prompt(
                    new MultiSelectionPrompt<string>()
                        .Title("Choose [mediumpurple2]projects[/] to run.")
                        .Required(true)
                        .PageSize(12)
                        .HighlightStyle(new Style(foreground: Color.MediumPurple2))
                        .MoreChoicesText("[grey](Move up and down to reveal more projects)[/]")
                        .InstructionsText(
                            "[grey](Press [mediumpurple2]<space>[/] to toggle a project, " +
                            "[green]<enter>[/] to accept)[/]")
                        .AddChoices(projects.Select(s => s.Name)));

                projects = projects.Where(x => choosedProjects.Contains(x.Name)).ToArray();
            }
        }
        else
        {
            projects = projects.Where(x => Projects.Any(y => x.FullName.Contains(y, StringComparison.InvariantCultureIgnoreCase))).ToArray();
        }

        var runConfiguration = new RunningConfiguration
        {
            EnvironmentName = EnvironmentName,
            SkipMigration = true,
            Watch = false,
            NoBuild = false,
            InstallLibs = true,
            GraphBuild = true,
            Retry = false,
            Projects = projects.ToDictionary(k => k.Directory.Name, v => new RunningConfigurationApp
            {
                Path = v.Directory.FullName,
                EnvironmentName = EnvironmentName,
                EnvironmentVariables = new Dictionary<string, string>()
                {
                    { "ASPNETCORE_ENVIRONMENT", EnvironmentName ?? "Development" },
                    { "ConnectionStrings__Default", $"Server=localhost;Database={v.Directory.Name};Trusted_Connection=True;MultipleActiveResultSets=true" }
                }
            })
        };

        var yamlContent = yamlSerializer.Serialize(runConfiguration);
        var path = Path.Combine(WorkingDirectory, Name + ".yml");
        File.WriteAllText(path, yamlContent);
    }
}
