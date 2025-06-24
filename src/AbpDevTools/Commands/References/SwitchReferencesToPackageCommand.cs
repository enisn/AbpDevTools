using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AbpDevTools.Configuration;
using AbpDevTools.Services;
using CliFx.Infrastructure;
using System.Xml.Linq;
using System.IO;
using Spectre.Console;

namespace AbpDevTools.Commands.References;

[Command("references to-package", Description = "Switch csproj local project references back to package references")]
public class SwitchReferencesToPackageCommand(
    LocalSourcesConfiguration localSourcesConfiguration,
    FileExplorer fileExplorer,
    CsprojManipulationService csprojService) : ICommand
{
    [CommandParameter(0, IsRequired = false, Description = "Working directory to run build. Probably project or solution directory path goes here. Default: . (Current Directory)")]
    public string? WorkingDirectory { get; set; }

    [CommandOption("sources", 's', Description = "Sources to switch to package. Default: all sources.")]
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

        // Cache for prompted versions to avoid asking multiple times for the same source
        var promptedVersions = new Dictionary<string, string>();

        foreach (var projectPath in projects)
        {
            await ProcessProjectAsync(projectPath, sourcesToProcess, projectLookupCache, promptedVersions, console);
        }

        console.Output.WriteLine("Completed switching references to package sources.");
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
        Dictionary<string, Dictionary<string, string>> projectLookupCache, 
        Dictionary<string, string> promptedVersions, IConsole console)
    {
        try
        {
            var doc = XDocument.Load(projectPath);
            var hasChanges = false;

            var projectReferences = csprojService.GetAllProjectReferences(doc);

            foreach (var projectRef in projectReferences.ToList()) // ToList() to avoid modification during enumeration
            {
                var referencedProjectPath = projectRef.Attribute("Include")?.Value;
                if (string.IsNullOrEmpty(referencedProjectPath)) continue;

                // Convert relative path to absolute path
                var absoluteReferencedPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectPath)!, referencedProjectPath));

                // Find which source this project belongs to (first match wins)
                var sourceKey = FindSourceForReferencedProject(absoluteReferencedPath, sources, projectLookupCache);
                if (string.IsNullOrEmpty(sourceKey)) continue;

                var projectName = Path.GetFileNameWithoutExtension(absoluteReferencedPath);

                // Check if this project matches any package patterns for this source
                var sourceConfig = sources.First(s => s.Key == sourceKey).Value;
                if (!DoesProjectMatchAnyPattern(projectName, sourceConfig.Packages)) continue;

                // Get version (from backup or prompt user)
                var version = GetVersionForSource(doc, sourceKey, promptedVersions);
                if (string.IsNullOrEmpty(version)) continue;

                // Convert to PackageReference
                csprojService.ConvertToPackageReference(projectRef, projectName, version);
                hasChanges = true;

                console.Output.WriteLine($"  Converted {projectName} to package reference with version {version}");
            }

            if (hasChanges)
            {
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

    private string? FindSourceForReferencedProject(string absoluteProjectPath, List<KeyValuePair<string, LocalSourceMappingItem>> sources, 
        Dictionary<string, Dictionary<string, string>> projectLookupCache)
    {
        var projectName = Path.GetFileNameWithoutExtension(absoluteProjectPath);

        // Check each source in order (first match wins)
        foreach (var source in sources)
        {
            var sourceKey = source.Key;
            var sourceConfig = source.Value;

            // Check if this project is in our lookup cache for this source
            if (projectLookupCache.TryGetValue(sourceKey, out var sourceProjects))
            {
                if (sourceProjects.TryGetValue(projectName, out var cachedProjectPath))
                {
                    // Verify the paths match
                    if (Path.GetFullPath(cachedProjectPath) == absoluteProjectPath)
                    {
                        return sourceKey;
                    }
                }
            }

            // Fallback: check if project is under source path
            if (csprojService.IsProjectUnderSource(absoluteProjectPath, sourceConfig.Path))
            {
                return sourceKey;
            }
        }

        return null;
    }

    private bool DoesProjectMatchAnyPattern(string projectName, HashSet<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (pattern.EndsWith("*"))
            {
                var prefix = pattern.Substring(0, pattern.Length - 1);
                if (projectName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else
            {
                if (string.Equals(projectName, pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private string? GetVersionForSource(XDocument doc, string sourceKey, Dictionary<string, string> promptedVersions)
    {
        // First, try to get from backed-up version
        var backedUpVersion = csprojService.GetBackedUpVersion(doc, sourceKey);
        if (!string.IsNullOrEmpty(backedUpVersion))
        {
            return backedUpVersion;
        }

        // Check if we already prompted for this source
        if (promptedVersions.TryGetValue(sourceKey, out var cachedVersion))
        {
            return cachedVersion;
        }

        // Prompt user for version
        var version = AnsiConsole.Ask<string>($"Enter version for source '[green]{sourceKey}[/]':");
        
        if (!string.IsNullOrEmpty(version))
        {
            promptedVersions[sourceKey] = version;
        }

        return version;
    }
} 