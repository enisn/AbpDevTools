using AbpDevTools.Configuration;
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
    protected IConsole? console;
    protected ToolsConfiguration ToolsConfiguration { get; }
    protected DotnetDependencyResolver DependencyResolver { get; }

    [CommandParameter(0, IsRequired = false, Description = "Working directory to run build. Probably project or solution directory path goes here. Default: . (Current Directory)")]
    public string? WorkingDirectory { get; set; }

    protected EnvironmentAppStartCommand EnvironmentAppStartCommand { get; }

    protected AbpBundleCommand AbpBundleCommand { get; }

    protected ToolOption Tools { get; }

    private readonly Dictionary<string, string> _packageToAppMapping = new()
    {
        ["Volo.Abp.EntityFrameworkCore.SqlServer"] = "sqlserver-edge",
        ["Volo.Abp.EntityFrameworkCore.MySQL"] = "mysql",
        ["Volo.Abp.EntityFrameworkCore.PostgreSql"] = "postgresql",
        ["Volo.Abp.Caching.StackExchangeRedis"] = "redis"
    };

    public PrepareCommand(
        EnvironmentAppStartCommand environmentAppStartCommand, 
        AbpBundleCommand abpBundleCommand,
        ToolsConfiguration toolsConfiguration,
        DotnetDependencyResolver dependencyResolver)
    {
        EnvironmentAppStartCommand = environmentAppStartCommand;
        AbpBundleCommand = abpBundleCommand;
        ToolsConfiguration = toolsConfiguration;
        DependencyResolver = dependencyResolver;

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

        var environmentApps = new List<string>();
        var installLibsFolders = new List<string>();
        var bundleFolders = new List<string>();

        await AnsiConsole.Status()
            .StartAsync("Checking projects for dependencies...", async ctx =>
            {
                foreach (var csproj in GetProjects())
                {
                    ctx.Status($"Checking {csproj.Name} for dependencies...");
                    foreach (var dependency in CheckEnvironmentApps(csproj.FullName))
                    {       
                        AnsiConsole.WriteLine($"{Emoji.Known.Package} '{dependency}' dependency found in {csproj.Name}");
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
            EnvironmentAppStartCommand.AppNames = environmentApps.Distinct().ToArray();

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
        await console.Output.WriteLineAsync("You can now run your application with 'abpdev run --env <env>'");
        await console.Output.WriteLineAsync("\n\tExample: 'abpdev run --env sqlserver'");
        await console.Output.WriteLineAsync("\n\n-----------------------------------------------------------");
        await console.Output.WriteLineAsync("You can check your current virtual environment with 'abpdev env' command.");
        await console.Output.WriteLineAsync("-----------------------------------------------------------");
    }

    private HashSet<string> CheckEnvironmentApps(string projectPath)
    {
        var results = new HashSet<string>();
        
        try
        {
            var dependencies = DependencyResolver.GetProjectDependencies(projectPath);
            
            foreach (var package in dependencies)
            {
                if (_packageToAppMapping.TryGetValue(package, out var appName))
                {
                    results.Add(appName);
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
