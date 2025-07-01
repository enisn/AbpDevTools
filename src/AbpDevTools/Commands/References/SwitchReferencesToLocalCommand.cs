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

[Command("references to-local", Description = "Switch csproj project references to local source")]
public class SwitchReferencesToLocalCommand : ICommand
{
    private readonly LocalSourcesConfiguration localSourcesConfiguration;
    private readonly FileExplorer fileExplorer;
    private readonly CsprojManipulationService csprojService;
    private readonly GitService gitService;

    public SwitchReferencesToLocalCommand(
        LocalSourcesConfiguration localSourcesConfiguration,
        FileExplorer fileExplorer,
        CsprojManipulationService csprojService,
        GitService gitService)
    {
        this.localSourcesConfiguration = localSourcesConfiguration ?? throw new ArgumentNullException(nameof(localSourcesConfiguration));
        this.fileExplorer = fileExplorer ?? throw new ArgumentNullException(nameof(fileExplorer));
        this.csprojService = csprojService ?? throw new ArgumentNullException(nameof(csprojService));
        this.gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
    }

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
        var projectLookupCache = await BuildProjectLookupCacheAsync(sourcesToProcess, console, cancellationToken);

        foreach (var projectPath in projects)
        {
            await ProcessProjectAsync(projectPath, sourcesToProcess, projectLookupCache, console);
        }

        console.Output.WriteLine("Completed switching references to local sources.");
    }

    private async Task<Dictionary<string, Dictionary<string, string>>> BuildProjectLookupCacheAsync(
        List<KeyValuePair<string, LocalSourceMappingItem>> sources, IConsole console, CancellationToken cancellationToken)
    {
        var cache = new Dictionary<string, Dictionary<string, string>>();

        foreach (var source in sources)
        {
            var sourceKey = source.Key;
            var sourceConfig = source.Value;
            
            console.Output.WriteLine($"Scanning source '{sourceKey}' at: {sourceConfig.Path}");
            
            if (!Directory.Exists(sourceConfig.Path) || gitService.IsDirectoryEmpty(sourceConfig.Path))
            {
                if (!Directory.Exists(sourceConfig.Path))
                {
                    console.Output.WriteLine($"  Warning: Source path does not exist: {sourceConfig.Path}");
                }
                else
                {
                    console.Output.WriteLine($"  Warning: Source path is empty: {sourceConfig.Path}");
                }

                // Prompt user for action when source is empty/missing
                var action = PromptForSourceActionAsync(sourceKey, sourceConfig, console);
                
                switch (action)
                {
                    case SourceAction.Skip:
                        console.Output.WriteLine($"  Skipping source '{sourceKey}' and continuing with next source...");
                        cache[sourceKey] = new Dictionary<string, string>();
                        continue;
                        
                    case SourceAction.OpenConfiguration:
                        console.Output.WriteLine($"Opening local sources configuration...");
                        var localSourcesCommand = new LocalSourcesCommand(localSourcesConfiguration);
                        await localSourcesCommand.ExecuteAsync(console);
                        return cache; // Exit the entire command
                        
                    case SourceAction.CloneRepository:
                        // Check if we can clone from remote
                        if (string.IsNullOrEmpty(sourceConfig.RemotePath))
                        {
                            AnsiConsole.MarkupLine($"[red]Error for source '{sourceKey}': No remote URL configured for cloning.[/]");
                            console.Output.WriteLine($"  Skipping source '{sourceKey}' and continuing with next source...");
                            cache[sourceKey] = new Dictionary<string, string>();
                            continue;
                        }

                        if (!gitService.IsGitInstalled())
                        {
                            AnsiConsole.MarkupLine($"[red]Error for source '{sourceKey}': Git is not installed or not available in PATH.[/]");
                            console.Output.WriteLine($"  Skipping source '{sourceKey}' and continuing with next source...");
                            cache[sourceKey] = new Dictionary<string, string>();
                            continue;
                        }

                        try
                        {
                            var branchInfo = !string.IsNullOrEmpty(sourceConfig.Branch) ? $" (branch: {sourceConfig.Branch})" : "";
                            var cloneSuccess = await gitService.CloneRepositoryAsync(sourceConfig.RemotePath, sourceConfig.Path, sourceConfig.Branch, cancellationToken);
                            
                            if (!cloneSuccess)
                            {
                                AnsiConsole.MarkupLine($"[red]Error for source '{sourceKey}': Failed to clone repository from {sourceConfig.RemotePath}[/]");
                                console.Output.WriteLine($"  Skipping source '{sourceKey}' and continuing with next source...");
                                cache[sourceKey] = new Dictionary<string, string>();
                                continue;
                            }
                            
                            AnsiConsole.MarkupLine($"[green]Successfully cloned source '{sourceKey}' from {sourceConfig.RemotePath}{branchInfo}[/]");
                        }
                        catch (Exception ex)
                        {
                            AnsiConsole.MarkupLine($"[red]Error cloning source '{sourceKey}': {ex.Message}[/]");
                            console.Output.WriteLine($"  Skipping source '{sourceKey}' and continuing with next source...");
                            cache[sourceKey] = new Dictionary<string, string>();
                            continue;
                        }
                        break;
                        
                    default:
                        console.Output.WriteLine($"  Skipping source '{sourceKey}' and continuing with next source...");
                        cache[sourceKey] = new Dictionary<string, string>();
                        continue;
                }
            }

            // Build the cache using the service
            var sourceCacheResult = csprojService.BuildProjectLookupCache(new List<KeyValuePair<string, LocalSourceMappingItem>> { source });
            if (sourceCacheResult.TryGetValue(sourceKey, out var sourceProjects))
            {
                cache[sourceKey] = sourceProjects;
            }
            else
            {
                cache[sourceKey] = new Dictionary<string, string>();
            }

            var projectCount = cache[sourceKey].Count;
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

    private SourceAction PromptForSourceActionAsync(string sourceKey, LocalSourceMappingItem sourceConfig, IConsole console)
    {
        var hasRemoteUrl = !string.IsNullOrEmpty(sourceConfig.RemotePath);
        var branchInfo = !string.IsNullOrEmpty(sourceConfig.Branch) ? $" (branch: {sourceConfig.Branch})" : "";
        
        AnsiConsole.MarkupLine($"[yellow]Source '{sourceKey}' is empty or doesn't exist at:[/] {sourceConfig.Path}");
        
        var choices = new List<string>
        {
            "Skip this source",
            "Open configuration to edit settings"
        };

        if (hasRemoteUrl)
        {
            choices.Add($"Clone from remote repository: {sourceConfig.RemotePath}{branchInfo}");
        }

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[bold]What would you like to do with source '[yellow]{sourceKey}[/]'?[/]")
                .AddChoices(choices)
        );

        return choice switch
        {
            "Skip this source" => SourceAction.Skip,
            "Open configuration to edit settings" => SourceAction.OpenConfiguration,
            var c when c.StartsWith("Clone from remote repository") => SourceAction.CloneRepository,
            _ => SourceAction.Skip
        };
    }
}