using AbpDevTools.Configuration;
using AbpDevTools.Environments;
using AbpDevTools.LocalConfigurations;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using Spectre.Console;
using System.Diagnostics;
using System.Text.Json;
using YamlDotNet.Core;

namespace AbpDevTools.Commands;

[Command("prepare", Description = "Prepare the project for the first running on this machine. Creates database, redis, event bus containers.")]
public class PrepareCommand : ICommand
{
    [CommandOption("no-config", Description = "Do not create local configuration file. (abpdev.yml)")]
    public bool NoConfiguration {get; set;} = false;

    protected IConsole? console;
    protected ToolsConfiguration ToolsConfiguration { get; }
    protected DotnetDependencyResolver DependencyResolver { get; }
    protected LocalConfigurationManager LocalConfigurationManager { get; }

    [CommandParameter(0, IsRequired = false, Description = "Working directory to run build. Probably project or solution directory path goes here. Default: . (Current Directory)")]
    public string? WorkingDirectory { get; set; }
    protected EnvironmentAppStartCommand EnvironmentAppStartCommand { get; }
    protected AbpBundleCommand AbpBundleCommand { get; }
    protected ToolOption Tools { get; }
    private readonly Dictionary<string, AppEnvironmentMapping> appEnvironmentMapping = AppEnvironmentMapping.Default;

    public PrepareCommand(
        EnvironmentAppStartCommand environmentAppStartCommand, 
        AbpBundleCommand abpBundleCommand,
        ToolsConfiguration toolsConfiguration,
        DotnetDependencyResolver dependencyResolver,
        LocalConfigurationManager localConfigurationManager)
    {
        EnvironmentAppStartCommand = environmentAppStartCommand;
        AbpBundleCommand = abpBundleCommand;
        ToolsConfiguration = toolsConfiguration;
        DependencyResolver = dependencyResolver;
        LocalConfigurationManager = localConfigurationManager;

        Tools = toolsConfiguration.GetOptions();
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        this.console = console;
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }

        AbpBundleCommand.WorkingDirectory = WorkingDirectory;

        var environmentApps = new List<AppEnvironmentMapping>();
        var installLibsFolders = new List<string>();
        var bundleFolders = new List<string>();

        await AnsiConsole.Status()
            .StartAsync("Checking projects for dependencies...", async ctx =>
            {
                foreach (var csproj in GetProjects())
                {
                    ctx.Status($"Checking {csproj.Name} for dependencies...");
                    var projectDependencies = CheckEnvironmentApps(csproj.FullName);
                    
                    foreach (var dependency in projectDependencies)
                    {       
                        AnsiConsole.WriteLine($"{Emoji.Known.Package} '{dependency.AppName}' dependency found in {csproj.Name}");
                        environmentApps.Add(dependency);
                    }
                }
            });

        if (environmentApps.Count == 0)
        {
            await console.Output.WriteLineAsync($"{Emoji.Known.Information} No environment apps required.");
        }
        else
        {
            if (!NoConfiguration)
            {
                var environmentNames = environmentApps.Where(x => !string.IsNullOrEmpty(x.EnvironmentName)).Select(x => x.EnvironmentName).Distinct().ToArray();
                if (environmentNames.Length > 1)
                {
                    AnsiConsole.WriteLine($"{Emoji.Known.CrossMark} [red]Multiple environments detected: {string.Join(", ", environmentNames)}[/] \n You can now run your application with 'abpdev run --env <env>'\n or run this command ('abpdev prepare') separately for each solution.");
                    AnsiConsole.WriteLine($"{Emoji.Known.Memo} You can skip creating local configuration file with '--no-config' option.");
                }
                else
                {
                    var environmentName = environmentNames.First();
                    var localConfig = new LocalConfiguration
                    {
                        Environment = new LocalConfiguration.LocalEnvironmentOption
                        {
                            Name = environmentName,
                        }
                    };

                    var filePath = LocalConfigurationManager.Save(WorkingDirectory, localConfig);
                    AnsiConsole.WriteLine($"{Emoji.Known.Memo} Created local configuration for environment {environmentName}: {Path.GetRelativePath(WorkingDirectory, filePath)}");
                }
            }

            EnvironmentAppStartCommand.AppNames = environmentApps.Select(x => x.AppName).Distinct().ToArray();

            await console.Output.WriteLineAsync("Starting required environment apps...");
            await console.Output.WriteLineAsync($"Apps to start: {string.Join(", ", environmentApps.Distinct())}");
            await console.Output.WriteLineAsync("-----------------------------------------------------------");

            await AnsiConsole.Status().StartAsync("Starting environment apps...", async ctx =>
            {
                await EnvironmentAppStartCommand.ExecuteAsync(console);
            });

            await console.Output.WriteLineAsync("Environment apps started successfully!");
        }

        await console.Output.WriteLineAsync("-----------------------------------------------------------");

        await AnsiConsole.Status().StartAsync("Installing libraries... (abp install-libs)", async ctx =>
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = Tools["abp"],
                Arguments = "install-libs",
                WorkingDirectory = WorkingDirectory,
                RedirectStandardOutput = true
            }) ?? throw new CommandException("Failed to start 'abp install-libs' process");

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            console.RegisterCancellationHandler().Register(() =>
            {
                console.Output.WriteLine("Abp install-libs cancelled.");
                process.Kill();
                cts.Cancel();
            });

            try 
            {
                await process.WaitForExitAsync(cts.Token);
                
                if (process.ExitCode != 0)
                {
                    throw new CommandException($"'abp install-libs' failed with exit code: {process.ExitCode}");
                }
            }
            catch (OperationCanceledException)
            {
                throw new CommandException("'abp install-libs' operation timed out or was cancelled.");
            }
        });


        await console.Output.WriteLineAsync("-----------------------------------------------------------");
        await console.Output.WriteLineAsync("Bundling Blazor WASM projects...");
        
        await AbpBundleCommand.ExecuteAsync(console);

        await console.Output.WriteLineAsync("-----------------------------------------------------------");
        await console.Output.WriteLineAsync($"{Emoji.Known.CheckBoxWithCheck} All done!");
        await console.Output.WriteLineAsync("-----------------------------------------------------------");
        await console.Output.WriteLineAsync("You can now run your application with 'abpdev run' without any environment specified.");
        await console.Output.WriteLineAsync("-----------------------------------------------------------");
    }

    private List<AppEnvironmentMapping> CheckEnvironmentApps(string projectPath)
    {
        var results = new List<AppEnvironmentMapping>();
        
        try
        {
            var dependencies = DependencyResolver.GetProjectDependencies(projectPath);
            
            foreach (var package in dependencies)
            {
                if (appEnvironmentMapping.TryGetValue(package, out var mapping))
                {
                    results.Add(mapping);
                }
            }
        }
        catch (Exception ex)
        {
            throw new CommandException($"Failed to analyze project dependencies: {ex.Message}");
        }

        return results;
    }

    private IEnumerable<FileInfo> GetProjects()
    {
        try 
        {
            return Directory.EnumerateFiles(WorkingDirectory!, "*.csproj", SearchOption.AllDirectories)
                        .Select(x => new FileInfo(x))
                        .ToArray();
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException || ex is UnauthorizedAccessException)
        {
            throw new CommandException($"Failed to enumerate project files: {ex.Message}");
        }
    }
}
