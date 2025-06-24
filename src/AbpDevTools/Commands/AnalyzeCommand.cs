using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CliFx.Infrastructure;
using Spectre.Console;

namespace AbpDevTools.Commands;

[Command("analyze", Description = "Analyze a DLL file for embedded resources and razor components (cshtml).")]
public class AnalyzeCommand : ICommand
{
    [CommandParameter(0, IsRequired = true, Description = "Package ID to analyze.")]
    public string PackageId { get; set; }

    [CommandParameter(1, IsRequired = false, Description = "Package version. If not provided, latest version will be used.")]
    public string? Version { get; set; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        // Get NuGet cache path
        var nugetCachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget",
            "packages",
            PackageId.ToLowerInvariant()
        );

        if (!Directory.Exists(nugetCachePath))
        {
            console.Output.WriteLine($"Package {PackageId} not found in NuGet cache.");
            return;
        }

        string? dllPath = null;
        if (string.IsNullOrEmpty(Version))
        {
            // Get latest version
            var versions = Directory.GetDirectories(nugetCachePath)
                .Select(d => new DirectoryInfo(d).Name)
                .OrderByDescending(v => System.Version.Parse(v))
                .ToList();

            if (!versions.Any())
            {
                console.Output.WriteLine($"No versions found for package {PackageId}.");
                return;
            }

            Version = versions.First();
            console.Output.WriteLine($"Using latest version: {Version}");
        }

        // Find the DLL in the package
        var packagePath = Path.Combine(nugetCachePath, Version);
        var dllFiles = Directory.GetFiles(packagePath, "*.dll", SearchOption.AllDirectories);

        if (!dllFiles.Any())
        {
            console.Output.WriteLine($"No DLL files found in package {PackageId} version {Version}.");
            return;
        }

        // If multiple DLLs found, let user choose
        if (dllFiles.Length > 1)
        {
            var selectedDll = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Multiple DLLs found. Select one to analyze:")
                    .PageSize(10)
                    .AddChoices(dllFiles.Select(f => Path.GetRelativePath(packagePath, f))));

            dllPath = Path.Combine(packagePath, selectedDll);
        }
        else
        {
            dllPath = dllFiles[0];
        }

        console.Output.WriteLine($"Analyzing {dllPath}");

        using var stream = File.OpenRead(dllPath);
        using var peReader = new PEReader(stream);
        var metadataReader = peReader.GetMetadataReader();

        // Get all type definitions
        var types = new List<string>();
        foreach (var typeDefHandle in metadataReader.TypeDefinitions)
        {
            var typeDef = metadataReader.GetTypeDefinition(typeDefHandle);
            var typeName = metadataReader.GetString(typeDef.Name);
            var namespaceName = metadataReader.GetString(typeDef.Namespace);
            var fullName = $"{namespaceName}.{typeName}";


            if (fullName.Contains("Views") 
            || fullName.Contains("Pages")
            || fullName.Contains("Layouts")
            || fullName.Contains("Components")
            )
            {
                types.Add(fullName);
            }
        }

        // Get all resources
        var resources = new List<string>();
        foreach (var resourceHandle in metadataReader.ManifestResources)
        {
            var resource = metadataReader.GetManifestResource(resourceHandle);
            var resourceName = metadataReader.GetString(resource.Name);
            resources.Add(resourceName);
        }

        // Display results
        if (types.Any())
        {
            console.Output.WriteLine($"\nFound {types.Count} potential views/pages:");
            foreach (var type in types)
            {
                console.Output.WriteLine($"  - {type}");
            }
        }
        else
        {
            console.Output.WriteLine("\nNo potential views/pages found.");
        }

        if (resources.Any())
        {
            console.Output.WriteLine($"\nFound {resources.Count} embedded resources:");
            foreach (var resource in resources)
            {
                console.Output.WriteLine($"  - {resource}");
            }
        }

        // Update analysis file if it exists
        var analysisFilePath = Path.Combine(packagePath, "content", $"{PackageId}.abppkg.analyze.json");
        if (File.Exists(analysisFilePath))
        {
            try
            {
                var jsonContent = await File.ReadAllTextAsync(analysisFilePath);
                var jsonObject = JsonNode.Parse(jsonContent)?.AsObject();
                
                if (jsonObject != null)
                {
                    var contentsArray = jsonObject["contents"]?.AsArray() ?? new JsonArray();
                    var existingContents = contentsArray.ToList();

                    foreach (var type in types)
                    {
                        var parts = type.Split('.');
                        var namespaceName = string.Join(".", parts.Take(parts.Length - 1));
                        var typeName = parts.Last();

                        // Check if this content already exists
                        var exists = existingContents.Any(c => 
                            c["namespace"]?.ToString() == namespaceName && 
                            c["name"]?.ToString() == typeName && 
                            c["contentType"]?.ToString() == "webPage");

                        if (!exists)
                        {
                            var newContent = new JsonObject
                            {
                                ["namespace"] = namespaceName,
                                ["contentType"] = "webPage",
                                ["name"] = typeName,
                                ["summary"] = null
                            };
                            contentsArray.Add(newContent);
                        }
                    }

                    // Write back to file with proper indentation
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var updatedJson = jsonObject.ToJsonString(options);
                    await File.WriteAllTextAsync(analysisFilePath, updatedJson);
                    console.Output.WriteLine($"\nUpdated analysis file at: {analysisFilePath}");
                }
            }
            catch (Exception ex)
            {
                console.Output.WriteLine($"\nWarning: Could not update analysis file: {ex.Message}");
            }
        }
    }
}