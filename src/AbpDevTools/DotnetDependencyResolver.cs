using AbpDevTools.Configuration;
using CliFx.Exceptions;
using Spectre.Console;
using System.Diagnostics;
using System.Text.Json;

namespace AbpDevTools;

[RegisterTransient]
public class DotnetDependencyResolver
{
    protected ToolOption Tools { get; }

    public DotnetDependencyResolver(ToolsConfiguration toolsConfiguration)
    {
        Tools = toolsConfiguration.GetOptions();
    }

    public async Task<bool> CheckSingleDependencyAsync(string projectPath, string assemblyName, CancellationToken cancellationToken)
    {
        return await IsPackageDependencyAsync(projectPath, assemblyName, cancellationToken);
    }

    private async Task<bool> IsPackageDependencyAsync(string projectPath, string assemblyName, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Tools["dotnet"],
            Arguments = $"nuget why \"{projectPath}\" \"{assemblyName}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30)); // 30-second timeout

        try
        {
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            // Wait for the process to exit or for the cancellation token to be triggered
            await Task.WhenAny(process.WaitForExitAsync(cts.Token), Task.Delay(Timeout.Infinite, cts.Token));

            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException($"'dotnet nuget why' command timed out for assembly '{assemblyName}' in project '{projectPath}'.");
            }

            var exitCode = process.ExitCode;
            var output = await outputTask;
            var error = await errorTask;

            if (exitCode != 0)
            {
                AnsiConsole.WriteLine($"Error executing 'dotnet nuget why': {error}");
                return false;
            }

            return !string.IsNullOrWhiteSpace(output);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            AnsiConsole.WriteLine($"'dotnet nuget why' command was cancelled for assembly '{assemblyName}' in project '{projectPath}'.");
            return false;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine($"Unexpected error executing 'dotnet nuget why': {ex.Message}");
            return false;
        }
    }

    public async Task<HashSet<string>> GetProjectDependenciesAsync(string projectPath)
    {
        var packages = new HashSet<string>();
        
        await RestoreProjectAsync(projectPath);
        
        // Get NuGet package references
        var listPackagesResult = await ExecuteDotnetListPackagesAsync(projectPath);
        var packageReferences = ParsePackageList(listPackagesResult);
        packages.UnionWith(packageReferences);

        // Get project references
        var listReferencesResult = await ExecuteDotnetListReferenceAsync(projectPath);
        var projectReferences = ParseProjectReferences(listReferencesResult);
        packages.UnionWith(projectReferences);

        return packages;
    }

    private async Task<string> ExecuteDotnetListPackagesAsync(string projectPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Tools["dotnet"],
            Arguments = $"list {projectPath} package --format json",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo) 
            ?? throw new CommandException("Failed to start 'dotnet list package' process.");

        // Asynchronously read output and error
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        // Wait for exit with timeout
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            process.WaitForExit(30000); // 30 seconds

            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException($"'dotnet list package' command timed out for project '{projectPath}'.");
            }

            if (process.ExitCode != 0)
            {
                var error = await errorTask;
                throw new CommandException($"'dotnet list package' failed with exit code {process.ExitCode}. Error: {error}");
            }

            return await outputTask;
        }
        catch (Exception ex) when (!(ex is CommandException))
        {
            process.Kill(entireProcessTree: true);
            throw new CommandException($"Error executing 'dotnet list package': {ex.Message}");
        }
    }

    private HashSet<string> ParsePackageList(string jsonOutput)
    {
        var packages = new HashSet<string>();
        
        try 
        {
            using var doc = JsonDocument.Parse(jsonOutput);
            var projects = doc.RootElement.GetProperty("projects");
            
            foreach (var project in projects.EnumerateArray())
            {
                if (project.TryGetProperty("frameworks", out var frameworks))
                {
                    foreach (var framework in frameworks.EnumerateArray())
                    {
                        if (framework.TryGetProperty("topLevelPackages", out var topLevelPackages))
                        {
                            foreach (var package in topLevelPackages.EnumerateArray())
                            {
                                var id = package.GetProperty("id").GetString();
                                if (!string.IsNullOrEmpty(id))
                                {
                                    packages.Add(id);
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            throw new CommandException("Failed to parse package list output.\n\n" + ex.Message);
        }
        
        return packages;
    }

    private async Task<string> ExecuteDotnetListReferenceAsync(string projectPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Tools["dotnet"],
            Arguments = $"list {projectPath} reference",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo) 
            ?? throw new CommandException("Failed to start 'dotnet list reference' process.");

        // Asynchronously read output and error
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        // Wait for exit with timeout
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            process.WaitForExit(30000); // 30 seconds

            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException($"'dotnet list reference' command timed out for project '{projectPath}'.");
            }

            if (process.ExitCode != 0)
            {
                var error = await errorTask;
                throw new CommandException($"'dotnet list reference' failed with exit code {process.ExitCode}. Error: {error}");
            }

            return await outputTask;
        }
        catch (Exception ex) when (!(ex is CommandException))
        {
            process.Kill(entireProcessTree: true);
            throw new CommandException($"Error executing 'dotnet list reference': {ex.Message}");
        }
    }

    private HashSet<string> ParseProjectReferences(string output)
    {
        var references = new HashSet<string>();
        
        var lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            var projectPath = line.Trim();
            var projectName = Path.GetFileNameWithoutExtension(projectPath);
            references.Add(projectName);
        }
        
        return references;
    }

    private async Task RestoreProjectAsync(string projectPath)
    {
        var friendlyProjectName = Path.GetFileNameWithoutExtension(projectPath);
        if(!ShouldRestoreProject(projectPath))
        {
            AnsiConsole.WriteLine($"{Emoji.Known.CheckMark}  Skipping restore for {friendlyProjectName} because it's already restored.");
            return;
        }

        AnsiConsole.WriteLine($"{Emoji.Known.RecyclingSymbol}  Restoring {friendlyProjectName}...");

        var restoreStartInfo = new ProcessStartInfo
        {
            FileName = Tools["dotnet"],
            Arguments = $"restore \"{projectPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var restoreProcess = Process.Start(restoreStartInfo) 
            ?? throw new CommandException("Failed to start 'dotnet restore' process.");

        // Asynchronously read output and error
        var outputTask = restoreProcess.StandardOutput.ReadToEndAsync();
        var errorTask = restoreProcess.StandardError.ReadToEndAsync();

        // Wait for exit with timeout
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120)); // 2-minute timeout
        try
        {
            restoreProcess.WaitForExit(120000); // 120 seconds

            if (!restoreProcess.HasExited)
            {
                restoreProcess.Kill(entireProcessTree: true);
                throw new TimeoutException($"'dotnet restore' command timed out for project '{friendlyProjectName}'.");
            }

            if (restoreProcess.ExitCode != 0)
            {
                var error = await errorTask;
                throw new CommandException($"'dotnet restore' failed with exit code {restoreProcess.ExitCode}. Error: {error}");
            }
        }
        catch (Exception ex) when (!(ex is CommandException))
        {
            restoreProcess.Kill(entireProcessTree: true);
            throw new CommandException($"Error executing 'dotnet restore': {ex.Message}");
        }
    }

    private bool ShouldRestoreProject(string projectPath)
    {
        var projectDirectory = Path.GetDirectoryName(projectPath) 
            ?? throw new CommandException($"Could not get directory for project: {projectPath}");

        var binPath = Path.Combine(projectDirectory, "bin");
        var objPath = Path.Combine(projectDirectory, "obj");

        if (!Directory.Exists(binPath) && !Directory.Exists(objPath))
        {
            return true;
        }

        var assetsFile = Path.Combine(objPath, "project.assets.json");
        if (!File.Exists(assetsFile))
        {
            return true;
        }

        var projectFileInfo = new FileInfo(projectPath);
        var assetsFileInfo = new FileInfo(assetsFile);

        return projectFileInfo.LastWriteTimeUtc > assetsFileInfo.LastWriteTimeUtc;
    }
} 