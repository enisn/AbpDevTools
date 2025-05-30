using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using Newtonsoft.Json;

namespace AbpPkgAnalyzer;

class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: AbpPkgAnalyzer <path-to-csproj-or-sln>");
            return;
        }

        var projectOrSolutionPath = args[0];
        MSBuildLocator.RegisterDefaults();

        using var workspace = MSBuildWorkspace.Create();
        Solution solution;
        if (projectOrSolutionPath.EndsWith(".sln"))
            solution = await workspace.OpenSolutionAsync(projectOrSolutionPath);
        else
            solution = (await workspace.OpenProjectAsync(projectOrSolutionPath)).Solution;

        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            if (compilation == null) continue;

            var modules = new List<object>();
            var webPages = new List<object>();

            INamedTypeSymbol? abpPageModelSymbol = compilation.GetTypeByMetadataName("Volo.Abp.AspNetCore.Mvc.UI.RazorPages.AbpPageModel");
            INamedTypeSymbol? abpComponentBaseSymbol = compilation.GetTypeByMetadataName("Volo.Abp.AspNetCore.Components.AbpComponentBase");

            foreach (var tree in compilation.SyntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(tree);
                var root = await tree.GetRootAsync();

                var classNodes = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>();
                foreach (var classNode in classNodes)
                {
                    var symbol = semanticModel.GetDeclaredSymbol(classNode) as INamedTypeSymbol;
                    if (symbol == null) continue;

                    // Check if inherits from AbpModule
                    if (InheritsFrom(symbol, "Volo.Abp.Modularity.AbpModule"))
                    {
                        var ns = symbol.ContainingNamespace.ToDisplayString();
                        var interfaces = symbol.AllInterfaces.Select(i => new
                        {
                            name = i.Name,
                            @namespace = i.ContainingNamespace.ToDisplayString(),
                            declaringAssemblyName = i.ContainingAssembly.Name,
                            fullName = i.ToDisplayString()
                        }).ToList();

                        var dependsOnModules = new List<object>();
                        foreach (var attr in symbol.GetAttributes())
                        {
                            if (attr.AttributeClass?.Name == "DependsOnAttribute")
                            {
                                foreach (var arg in attr.ConstructorArguments)
                                {
                                    foreach (var moduleType in arg.Values)
                                    {
                                        var moduleSymbol = moduleType.Value as INamedTypeSymbol;
                                        if (moduleSymbol != null)
                                        {
                                            dependsOnModules.Add(new
                                            {
                                                declaringAssemblyName = moduleSymbol.ContainingAssembly.Name,
                                                @namespace = moduleSymbol.ContainingNamespace.ToDisplayString(),
                                                name = moduleSymbol.Name
                                            });
                                        }
                                    }
                                }
                            }
                        }

                        modules.Add(new
                        {
                            @namespace = ns,
                            contentType = "abpModule",
                            name = symbol.Name,
                            dependsOnModules,
                            implementingInterfaces = interfaces
                        });
                    }

                    // --- Razor Page logic ---
                    if (abpPageModelSymbol != null && InheritsFrom(symbol, abpPageModelSymbol))
                    {
                        webPages.Add(new
                        {
                            contentType = "webPage",
                            name = symbol.Name,
                            @namespace = symbol.ContainingNamespace.ToDisplayString()
                        });
                    }

                    // --- Blazor Component logic ---
                    if (abpComponentBaseSymbol != null && InheritsFrom(symbol, abpComponentBaseSymbol))
                    {
                        webPages.Add(new
                        {
                            contentType = "webPage",
                            name = symbol.Name,
                            @namespace = symbol.ContainingNamespace.ToDisplayString()
                        });
                    }
                }
            }

            // --- Razor file enumeration ---
            var projectDir = Path.GetDirectoryName(project.FilePath)!;
            // Enumerate .cshtml files (Razor Pages)
            foreach (var cshtmlFile in Directory.EnumerateFiles(projectDir, "*.cshtml", SearchOption.AllDirectories))
            {
                webPages.Add(new
                {
                    contentType = "razorPage",
                    name = Path.GetFileNameWithoutExtension(cshtmlFile),
                    filePath = Path.GetRelativePath(projectDir, cshtmlFile)
                });
            }
            // Enumerate .razor files (Blazor Components)
            foreach (var razorFile in Directory.EnumerateFiles(projectDir, "*.razor", SearchOption.AllDirectories))
            {
                webPages.Add(new
                {
                    contentType = "razorComponent",
                    name = Path.GetFileNameWithoutExtension(razorFile),
                    filePath = Path.GetRelativePath(projectDir, razorFile)
                });
            }

            var contents = modules.Concat(webPages).ToList();
            if (contents.Count > 0)
            {
                var output = new
                {
                    name = project.AssemblyName,
                    hash = "",
                    contents = contents
                };

                var json = JsonConvert.SerializeObject(output, Formatting.Indented);
                var outPath = Path.Combine(Path.GetDirectoryName(project.FilePath)!, $"{project.AssemblyName}.abppkg.analyze.json");
                File.WriteAllText(outPath, json);
                Console.WriteLine($"Written: {outPath} (Modules: {modules.Count}, WebPages: {webPages.Count})");
            }
            else
            {
                Console.WriteLine($"No modules or web pages found in {project.Name}");
            }
        }
    }

    static bool InheritsFrom(INamedTypeSymbol symbol, string baseTypeFullName)
    {
        var baseType = symbol.BaseType;
        while (baseType != null)
        {
            if (baseType.ToDisplayString() == baseTypeFullName)
                return true;
            baseType = baseType.BaseType;
        }
        return false;
    }

    static bool InheritsFrom(INamedTypeSymbol symbol, INamedTypeSymbol baseType)
    {
        var current = symbol.BaseType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
            current = current.BaseType;
        }
        return false;
    }
}
