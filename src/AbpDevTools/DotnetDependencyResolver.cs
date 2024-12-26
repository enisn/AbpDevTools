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

    public HashSet<string> GetProjectDependencies(string projectPath)
    {
        var packages = new HashSet<string>();
        
        RestoreProject(projectPath);
        
        // Get NuGet package references
        var listPackagesResult = ExecuteDotnetListPackages(projectPath);
        var packageReferences = ParsePackageList(listPackagesResult);
        packages.UnionWith(packageReferences);

        // Get project references
        var listReferencesResult = ExecuteDotnetListReference(projectPath);
        var projectReferences = ParseProjectReferences(listReferencesResult);
        packages.UnionWith(projectReferences);

        return packages;
    }

    private string ExecuteDotnetListPackages(string projectPath)
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
            ?? throw new CommandException("Failed to start dotnet list package process");

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            throw new CommandException($"dotnet list package failed with exit code {process.ExitCode}. Error: {error}");
        }

        return process.StandardOutput.ReadToEnd();
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

    private string ExecuteDotnetListReference(string projectPath)
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
            ?? throw new CommandException("Failed to start dotnet list reference process");

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            throw new CommandException($"dotnet list reference failed with exit code {process.ExitCode}. Error: {error}");
        }

        return process.StandardOutput.ReadToEnd();
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

    private void RestoreProject(string projectPath)
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
            Arguments = $"restore {projectPath}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var restoreProcess = Process.Start(restoreStartInfo) 
            ?? throw new CommandException("Failed to start dotnet restore process");

        var outputTask = restoreProcess.StandardOutput.ReadToEndAsync();
        var errorTask = restoreProcess.StandardError.ReadToEndAsync();

        if (!restoreProcess.WaitForExit(120 * 1000))
        {
            restoreProcess.Kill();
            throw new CommandException($"Restore operation timed out for {friendlyProjectName}");
        }

        var error = errorTask.Result;
        if (restoreProcess.ExitCode != 0)
        {
            throw new CommandException($"dotnet restore failed with exit code {restoreProcess.ExitCode}. Error: {error}");
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