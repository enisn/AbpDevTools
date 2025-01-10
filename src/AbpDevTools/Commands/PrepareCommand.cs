using AbpDevTools.Configuration;
using AbpDevTools.Environments;
using AbpDevTools.LocalConfigurations;
using AbpDevTools.Services;
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
    protected RunnableProjectsProvider RunnableProjectsProvider { get; }

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
        RunnableProjectsProvider runnableProjectsProvider,
        LocalConfigurationManager localConfigurationManager)
    {
        EnvironmentAppStartCommand = environmentAppStartCommand;
        AbpBundleCommand = abpBundleCommand;
        ToolsConfiguration = toolsConfiguration;
        DependencyResolver = dependencyResolver;
        RunnableProjectsProvider = runnableProjectsProvider;
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

        var cancellationToken = console.RegisterCancellationHandler();

        var environmentAppsPerProject = new Dictionary<string, List<AppEnvironmentMapping>>();
        var installLibsFolders = new List<string>();
        var bundleFolders = new List<string>();

        await AnsiConsole.Status()
            .StartAsync("Checking projects for dependencies...", async ctx =>
            {
                var runnableProjects = RunnableProjectsProvider.GetRunnableProjects(WorkingDirectory);
                foreach (var csproj in runnableProjects)
                {
                    ctx.Status($"Checking {csproj.Name} for dependencies...");
                    var projectDependencies = await CheckEnvironmentAppsAsync(csproj.FullName, cancellationToken);
                    
                    var dependencies = new List<AppEnvironmentMapping>();
                    foreach (var dependency in projectDependencies)
                    {       
                        dependencies.Add(dependency);
                    }

                    if (dependencies.Count > 0)
                    {
                        AnsiConsole.WriteLine($"{Emoji.Known.Package} {string.Join(", ", dependencies.Select(x => x.AppName))} (total: {dependencies.Count}) dependencies found for {csproj.Name}");
                        environmentAppsPerProject[csproj.FullName] = dependencies;
                    }
                }
            });

        if (environmentAppsPerProject.Count(x => x.Value.Count > 0) == 0)
        {
            await console.Output.WriteLineAsync($"{Emoji.Known.Information} No environment apps required.");
        }
        else
        {
            var environmentApps = environmentAppsPerProject.Values.SelectMany(x => x).Distinct().ToArray();
            if (!NoConfiguration)
            {
                var environmentNames = environmentApps.Where(x => !string.IsNullOrEmpty(x.EnvironmentName)).Select(x => x.EnvironmentName).Distinct().ToArray();
                if (environmentNames.Length > 1)
                {
                    AnsiConsole.WriteLine($"{Emoji.Known.CrossMark} [red]Multiple environments detected: {string.Join(", ", environmentNames)}[/] \n You can now run your application with 'abpdev run --env <env>'\n or run this command ('abpdev prepare') separately for each solution.");
                    AnsiConsole.WriteLine($"{Emoji.Known.Information} Here is the list of running commands for each environment:");
                    foreach (var env in environmentNames)
                    {
                        AnsiConsole.WriteLine($"\tabpdev run --env {env}");
                    }
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
                AnsiConsole.WriteLine($"{Emoji.Known.Memo} You can skip creating local configuration file with '--no-config' option.");
            }

            EnvironmentAppStartCommand.AppNames = environmentApps.Select(x => x.AppName).ToArray();

            await console.Output.WriteLineAsync("Starting required environment apps...");
            await console.Output.WriteLineAsync($"Apps to start: {string.Join(", ", environmentApps.Select(x => x.AppName))}");
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
            var process = new ProcessStartInfo
            {
                FileName = Tools["abp"],
                Arguments = "install-libs",
                WorkingDirectory = WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var installLibsProcess = new Process { StartInfo = process };
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMinutes(5));

            try
            {
                installLibsProcess.Start();

                var outputTask = installLibsProcess.StandardOutput.ReadToEndAsync();
                var errorTask = installLibsProcess.StandardError.ReadToEndAsync();

                // Wait for the process to exit or for the cancellation token to be triggered
                var waitTask = installLibsProcess.WaitForExitAsync(cts.Token);

                if (await Task.WhenAny(waitTask, Task.Delay(Timeout.Infinite, cts.Token)) != waitTask)
                {
                    installLibsProcess.Kill(entireProcessTree: true);
                    throw new TimeoutException("'abp install-libs' command timed out.");
                }

                var exitCode = installLibsProcess.ExitCode;
                var output = await outputTask;
                var error = await errorTask;

                if (exitCode != 0)
                {
                    AnsiConsole.WriteLine($"Error executing 'abp install-libs': {error}");
                    throw new CommandException($"'abp install-libs' failed with exit code: {exitCode}");
                }
            }
            catch (OperationCanceledException)
            {
                if (!installLibsProcess.HasExited)
                {
                    installLibsProcess.Kill(entireProcessTree: true);
                }
                AnsiConsole.WriteLine("'abp install-libs' command was cancelled.");
                throw new CommandException("'abp install-libs' operation was cancelled.");
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteLine($"Unexpected error executing 'abp install-libs': {ex.Message}");
                throw;
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

    private async Task<List<AppEnvironmentMapping>> CheckEnvironmentAppsAsync(string projectPath, CancellationToken cancellationToken)
    {
        var results = new List<AppEnvironmentMapping>();
        var tasks = appEnvironmentMapping.Keys.Select(async package =>
        {
            try
            {
                bool hasDependency = await DependencyResolver.CheckSingleDependencyAsync(projectPath, package, cancellationToken);
                if (hasDependency && appEnvironmentMapping.TryGetValue(package, out var mapping))
                {
                    results.Add(mapping);
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteLine($"Error checking dependency '{package}' in project '{Path.GetFileName(projectPath)}': {ex.Message}");
                // Optionally log the exception or handle it as needed
            }
        });

        await Task.WhenAll(tasks);
        return results;
    }
}
