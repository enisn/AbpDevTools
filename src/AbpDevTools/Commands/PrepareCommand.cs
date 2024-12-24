﻿using CliFx.Exceptions;
using CliFx.Infrastructure;
using Spectre.Console;
using System.Diagnostics;
using System.Text.Json;

namespace AbpDevTools.Commands;

[Command("prepare", Description = "Prepare the project for the first running on this machine. Creates database, redis, event bus containers.")]
public class PrepareCommand : ICommand
{
    protected IConsole? console;

    [CommandParameter(0, IsRequired = false, Description = "Working directory to run build. Probably project or solution directory path goes here. Default: . (Current Directory)")]
    public string? WorkingDirectory { get; set; }

    protected EnvironmentAppStartCommand EnvironmentAppStartCommand { get; }

    protected AbpBundleCommand AbpBundleCommand { get; }

    private readonly Dictionary<string, string> _packageToAppMapping = new()
    {
        ["Volo.Abp.EntityFrameworkCore.SqlServer"] = "sqlserver-edge",
        ["Volo.Abp.EntityFrameworkCore.MySQL"] = "mysql",
        ["Volo.Abp.EntityFrameworkCore.PostgreSql"] = "postgresql",
        ["Volo.Abp.Caching.StackExchangeRedis"] = "redis"
    };

    public PrepareCommand(EnvironmentAppStartCommand environmentAppStartCommand, AbpBundleCommand abpBundleCommand)
    {
        EnvironmentAppStartCommand = environmentAppStartCommand;
        AbpBundleCommand = abpBundleCommand;
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

        foreach (var csproj in GetProjects())
        {
            environmentApps.AddRange(CheckEnvironmentApps(csproj.FullName));
        }

        if (environmentApps.Count > 0)
        {
            EnvironmentAppStartCommand.AppNames = environmentApps.Distinct().ToArray();

            await console.Output.WriteLineAsync("Starting required environment apps...");
            await console.Output.WriteLineAsync($"Apps to start: {string.Join(", ", environmentApps.Distinct())}");
            await console.Output.WriteLineAsync("-----------------------------------------------------------");

            await EnvironmentAppStartCommand.ExecuteAsync(console);

            await console.Output.WriteLineAsync("Environment apps started successfully!");
        }

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "abp",
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

        await AbpBundleCommand.ExecuteAsync(console);

        await console.Output.WriteLineAsync("-----------------------------------------------------------");
        await console.Output.WriteLineAsync("✅ All done!");
        await console.Output.WriteLineAsync("-----------------------------------------------------------");
        await console.Output.WriteLineAsync("You can now run your application with 'abpdev run --env <env>'");
        await console.Output.WriteLineAsync("\n\tExample: 'abpdev run --env sqlserver'");
        await console.Output.WriteLineAsync("\n\n-----------------------------------------------------------");
        await console.Output.WriteLineAsync("You can check your current virtual environment with 'abpdev env' command.");
        await console.Output.WriteLineAsync("-----------------------------------------------------------");
    }

    private IEnumerable<string> CheckEnvironmentApps(string projectPath)
    {
        var results = new List<string>();
        
        try
        {
            var dependencies = GetProjectDependencies(projectPath);
            
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

    private HashSet<string> GetProjectDependencies(string projectPath)
    {
        var _packages = new HashSet<string>();
        
        RestoreProject(projectPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"list {projectPath} package --format json",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new CommandException("Failed to start dotnet process");
        }

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new CommandException($"dotnet list package failed with exit code {process.ExitCode}{(process.ExitCode == 1 ? ". Make sure the project is restored" : string.Empty)}");
        }

        try
        {
            using var doc = JsonDocument.Parse(output);
            var projects = doc.RootElement.GetProperty("projects");
            
            foreach (var project in projects.EnumerateArray())
            {
                if (project.TryGetProperty("frameworks", out var frameworks))
                {
                    foreach (var framework in frameworks.EnumerateArray())
                    {
                        if (framework.TryGetProperty("topLevelPackages", out var packages))
                        {
                            foreach (var package in packages.EnumerateArray())
                            {
                                var id = package.GetProperty("id").GetString();
                                if (!string.IsNullOrEmpty(id))
                                {
                                    _packages.Add(id);
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            throw new CommandException($"Failed to parse package list JSON: {ex.Message}");
        }

        return _packages;
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
    

    private void RestoreProject(string projectPath)
    {
        var restoreStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"restore {projectPath}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var restoreProcess = Process.Start(restoreStartInfo) 
            ?? throw new CommandException("Failed to start dotnet restore process");

        restoreProcess.WaitForExit();

        if (restoreProcess.ExitCode != 0)
        {
            var error = restoreProcess.StandardError.ReadToEnd();
            throw new CommandException($"dotnet restore failed with exit code {restoreProcess.ExitCode}. Error: {error}");
        }
    }
}
