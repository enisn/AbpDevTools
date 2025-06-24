using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AbpDevTools.Configuration;
using AbpDevTools.Services;
using CliFx.Infrastructure;
using System.Xml.Linq;
using System.IO;

namespace AbpDevTools.Commands.References;

[Command("references to-local", Description = "Switch csproj project references to local source")]
public class SwitchReferencesToLocalCommand(
    LocalSourcesConfiguration localSourcesConfiguration,
    FileExplorer fileExplorer,
    CsprojManipulationService csprojService) : ICommand
{
    [CommandParameter(0, IsRequired = false, Description = "Working directory to run build. Probably project or solution directory path goes here. Default: . (Current Directory)")]
    public string? WorkingDirectory { get; set; }

    [CommandOption("sources", 's', Description = "Sources to switch to local. Default: all sources.")]
    public string[] Sources { get; set; } = Array.Empty<string>();

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var localSources = localSourcesConfiguration.GetOptions();

        if (localSources.Count == 0)
        {
            console.Output.WriteLine("No local sources found. Please add some local sources to the configuration. Use 'abpdev local-sources config' command to manage local sources.");
            return;
        }

        var cancellationToken = console.RegisterCancellationHandler();
        cancellationToken.ThrowIfCancellationRequested();
        WorkingDirectory ??= Directory.GetCurrentDirectory();

        var projects = fileExplorer.FindDescendants(WorkingDirectory, "*.csproj").ToList();

        if (!projects.Any())
        {
            console.Output.WriteLine("No .csproj files found in the working directory.");
            return;
        }

        // Filter sources if specified
        var sourcesToProcess = Sources.Length > 0 
            ? localSources.Where(kvp => Sources.Contains(kvp.Key)).ToList()
            : localSources.ToList();

        if (!sourcesToProcess.Any())
        {
            console.Output.WriteLine("No matching sources found.");
            return;
        }

        console.Output.WriteLine($"Processing {projects.Count} project(s) with {sourcesToProcess.Count} source(s)...");

        // Build project lookup cache for all sources
        var projectLookupCache = BuildProjectLookupCache(sourcesToProcess, console);

        foreach (var projectPath in projects)
        {
            await ProcessProjectAsync(projectPath, sourcesToProcess, projectLookupCache, console);
        }

        console.Output.WriteLine("Completed switching references to local sources.");
    }

    private Dictionary<string, Dictionary<string, string>> BuildProjectLookupCache(
        List<KeyValuePair<string, LocalSourceMappingItem>> sources, IConsole console)
    {
        var cache = csprojService.BuildProjectLookupCache(sources);

        foreach (var source in sources)
        {
            var sourceKey = source.Key;
            var sourceConfig = source.Value;
            
            console.Output.WriteLine($"Scanning source '{sourceKey}' at: {sourceConfig.Path}");
            
            if (!Directory.Exists(sourceConfig.Path))
            {
                console.Output.WriteLine($"  Warning: Source path does not exist: {sourceConfig.Path}");
                continue;
            }

            var projectCount = cache.ContainsKey(sourceKey) ? cache[sourceKey].Count : 0;
            console.Output.WriteLine($"  Found {projectCount} project(s) in source '{sourceKey}'");
        }

        return cache;
    }

    private Task ProcessProjectAsync(string projectPath, List<KeyValuePair<string, LocalSourceMappingItem>> sources, 
        Dictionary<string, Dictionary<string, string>> projectLookupCache, IConsole console)
    {
        try
        {
            var doc = XDocument.Load(projectPath);
            var hasChanges = false;
            var versionsToBackup = new Dictionary<string, string>();

            foreach (var source in sources)
            {
                var sourceKey = source.Key;
                var sourceConfig = source.Value;

                foreach (var packagePattern in sourceConfig.Packages)
                {
                    var packageReferences = csprojService.GetMatchingPackageReferences(doc, packagePattern);

                    foreach (var packageRef in packageReferences)
                    {
                        var packageName = packageRef.Attribute("Include")?.Value;
                        if (string.IsNullOrEmpty(packageName)) continue;

                        var localProjectPath = csprojService.FindLocalProject(packageName, sourceKey, projectLookupCache);
                        if (string.IsNullOrEmpty(localProjectPath)) continue;

                        // Calculate relative path
                        var relativePath = csprojService.GetRelativePath(Path.GetDirectoryName(projectPath)!, localProjectPath);

                        // Backup version
                        var version = packageRef.Attribute("Version")?.Value;
                        if (!string.IsNullOrEmpty(version) && !versionsToBackup.ContainsKey(sourceKey))
                        {
                            versionsToBackup[sourceKey] = version;
                        }

                        // Convert to ProjectReference
                        csprojService.ConvertToProjectReference(packageRef, relativePath);
                        hasChanges = true;

                        console.Output.WriteLine($"  Converted {packageName} to local reference: {relativePath}");
                    }
                }
            }

            if (hasChanges)
            {
                // Add version backup properties
                csprojService.AddVersionBackupProperties(doc, versionsToBackup);
                doc.Save(projectPath);
                console.Output.WriteLine($"Updated: {Path.GetFileName(projectPath)}");
            }
        }
        catch (Exception ex)
        {
            console.Output.WriteLine($"Error processing {Path.GetFileName(projectPath)}: {ex.Message}");
        }
        return Task.CompletedTask;
    }
}