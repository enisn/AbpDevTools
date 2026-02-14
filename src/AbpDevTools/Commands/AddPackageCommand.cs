using System.Diagnostics;
using System.Xml.Linq;
using AbpDevTools.Services;
using CliFx.Infrastructure;
using Spectre.Console;

namespace AbpDevTools.Commands;

[Command("add-package", Description = "Adds a NuGet package to the project and configures module dependency. Works with any NuGet source configured in nuget.config.")]
public class AddPackageCommand : ICommand
{
    [CommandParameter(0, Description = "Name of the NuGet package to add.")]
    public string PackageName { get; set; } = string.Empty;

    [CommandOption("project", 'p', Description = "Path to the project file (.csproj). Default: searches current directory.")]
    public string? ProjectPath { get; set; }

    [CommandOption("version", 'v', Description = "Version of the package to install.")]
    public string? Version { get; set; }

    [CommandOption("skip-dependency", 's', Description = "Skip adding module dependency (DependsOn attribute).")]
    public bool SkipDependency { get; set; }

    [CommandOption("no-restore", Description = "Skip restoring the project after adding the package.")]
    public bool NoRestore { get; set; }

    [CommandOption("all", 'a', Description = "Add package to all projects in the solution/folder.")]
    public bool AddToAll { get; set; }

    private readonly AssemblyModuleFinder _assemblyModuleFinder;
    private readonly SourceCodeModuleFinder _sourceCodeModuleFinder;
    private readonly ModuleDependencyAdder _moduleDependencyAdder;
    private readonly FileExplorer _fileExplorer;

    public AddPackageCommand(
        AssemblyModuleFinder assemblyModuleFinder,
        SourceCodeModuleFinder sourceCodeModuleFinder,
        ModuleDependencyAdder moduleDependencyAdder,
        FileExplorer fileExplorer)
    {
        _assemblyModuleFinder = assemblyModuleFinder;
        _sourceCodeModuleFinder = sourceCodeModuleFinder;
        _moduleDependencyAdder = moduleDependencyAdder;
        _fileExplorer = fileExplorer;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var cancellationToken = console.RegisterCancellationHandler();

        var projectFiles = await FindProjectFilesAsync();
        
        if (projectFiles.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Could not find any .csproj files.");
            return;
        }

        if (projectFiles.Length > 1 && !AddToAll && string.IsNullOrEmpty(ProjectPath))
        {
            var chosenProjects = AnsiConsole.Prompt(new MultiSelectionPrompt<string>()
                .Title("Choose projects to add the package.")
                .Required(true)
                .PageSize(12)
                .HighlightStyle(new Style(foreground: Color.MediumPurple2))
                .MoreChoicesText("[grey](Move up and down to reveal more projects)[/]")
                .InstructionsText(
                    "[grey](Press [mediumpurple2]<space>[/] to toggle a project, " +
                    "[green]<enter>[/] to accept)[/]")
                .AddChoices(projectFiles
                    .Select(p => GetRelativePath(p))
                    .ToArray())
            );

            projectFiles = projectFiles.Where(p => chosenProjects.Any(cp => GetRelativePath(p).Equals(cp, StringComparison.OrdinalIgnoreCase))).ToArray();
        }

        AnsiConsole.MarkupLine($"[green]Selected {projectFiles.Length} project(s)[/]");

        foreach (var projectFile in projectFiles)
        {
            AnsiConsole.MarkupLine($"  [cyan]•[/] {Markup.Escape(GetRelativePath(projectFile))}");
        }

        foreach (var projectFile in projectFiles)
        {
            await AnsiConsole.Status()
                .StartAsync($"Adding package [cyan]{Markup.Escape(PackageName)}[/] to [yellow]{Markup.Escape(Path.GetFileName(projectFile))}[/]...", async ctx =>
                {
                    await AddPackageAsync(projectFile, ctx);
                });

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (SkipDependency)
            {
                AnsiConsole.MarkupLine("[yellow]Skipping module dependency configuration.[/]");
                continue;
            }

            if (!NoRestore)
            {
                await AnsiConsole.Status()
                    .StartAsync("Restoring packages...", async ctx =>
                    {
                        await RestoreProjectAsync(projectFile, ctx);
                    });
            }

            await AnsiConsole.Status()
                .StartAsync("Configuring module dependency...", async ctx =>
                {
                    await ConfigureModuleDependencyAsync(projectFile, ctx, cancellationToken);
                });
        }
    }

    private string GetRelativePath(string fullPath)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var relativePath = fullPath.Replace(currentDir, string.Empty).Trim(Path.DirectorySeparatorChar, '/');
        return string.IsNullOrEmpty(relativePath) ? Path.GetFileName(fullPath) : relativePath;
    }

    private async Task<string[]> FindProjectFilesAsync()
    {
        if (!string.IsNullOrEmpty(ProjectPath))
        {
            if (File.Exists(ProjectPath))
            {
                return new[] { Path.GetFullPath(ProjectPath) };
            }

            AnsiConsole.MarkupLine($"[red]Error:[/] Project file not found: {Markup.Escape(ProjectPath)}");
            return Array.Empty<string>();
        }

        var currentDir = Directory.GetCurrentDirectory();
        var projectFiles = _fileExplorer.FindDescendants(currentDir, "*.csproj")
            .OrderBy(p => p)
            .ToArray();

        return projectFiles;
    }

    private async Task AddPackageAsync(string projectFile, StatusContext ctx)
    {
        var arguments = $"add \"{projectFile}\" package {PackageName}";
        
        if (!string.IsNullOrEmpty(Version))
        {
            arguments += $" --version {Version}";
        }

        ctx.Status($"Running: [grey]dotnet {Markup.Escape(arguments)}[/]");

        var result = await RunDotnetCommandAsync(arguments, Path.GetDirectoryName(projectFile)!);

        if (result.ExitCode != 0)
        {
            AnsiConsole.MarkupLine($"[red]Error adding package:[/]");
            AnsiConsole.WriteLine(result.Output);
            throw new CommandException("Failed to add package.");
        }

        AnsiConsole.MarkupLine($"[green]Package added:[/] {Markup.Escape(PackageName)}");
    }

    private async Task RestoreProjectAsync(string projectFile, StatusContext ctx)
    {
        ctx.Status("Restoring packages...");

        var arguments = $"restore \"{projectFile}\"";
        var result = await RunDotnetCommandAsync(arguments, Path.GetDirectoryName(projectFile)!);

        if (result.ExitCode != 0)
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] Package restore may have issues.");
            AnsiConsole.WriteLine(result.Output);
        }
        else
        {
            AnsiConsole.MarkupLine("[green]Packages restored.[/]");
        }
    }

    private async Task ConfigureModuleDependencyAsync(string projectFile, StatusContext ctx, CancellationToken cancellationToken)
    {
        ctx.Status("Finding project's module class...");

        var projectModules = _sourceCodeModuleFinder.FindAbpModuleClasses(projectFile);

        if (projectModules.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] No ABP module class found in the project. Skipping dependency configuration.");
            return;
        }

        if (projectModules.Count > 1)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] Multiple module classes found. Using first one: {Markup.Escape(projectModules[0].Name)}");
        }

        var targetModule = projectModules[0];
        AnsiConsole.MarkupLine($"[green]Found module:[/] {Markup.Escape(targetModule.FullName)}");

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        ctx.Status($"Finding module class in package {Markup.Escape(PackageName)}...");

        var packageModule = await FindPackageModuleClassAsync(projectFile, ctx);

        if (packageModule == null)
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] Could not find an ABP module class in the package. Skipping dependency configuration.");
            AnsiConsole.MarkupLine("[grey]The package may not be an ABP module, or the DLL could not be analyzed.[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[green]Found package module:[/] {Markup.Escape(packageModule.FullName)}");

        ctx.Status($"Adding DependsOn attribute...");

        _moduleDependencyAdder.AddDependency(targetModule.FilePath, packageModule.FullName, checkExisting: true);

        AnsiConsole.MarkupLine($"[green]Added dependency:[/] {Markup.Escape($"[DependsOn(typeof({packageModule.Name}))]")}");
    }

    private async Task<ModuleTypeInfo?> FindPackageModuleClassAsync(string projectFile, StatusContext ctx)
    {
        var packageVersion = GetPackageVersionFromProject(projectFile, PackageName);
        
        var packageDlls = FindPackageDllsInCache(PackageName, packageVersion);

        foreach (var dll in packageDlls)
        {
            ctx.Status($"Analyzing: {Markup.Escape(Path.GetFileName(dll))}...");
            
            var module = _assemblyModuleFinder.FindAbpModuleClass(dll);
            
            if (module != null)
            {
                return module;
            }
        }

        return null;
    }

    private string? GetPackageVersionFromProject(string projectFile, string packageName)
    {
        try
        {
            var doc = XDocument.Load(projectFile);
            var packageRef = doc.Descendants("PackageReference")
                .FirstOrDefault(pr => 
                    string.Equals(pr.Attribute("Include")?.Value, packageName, StringComparison.OrdinalIgnoreCase));

            return packageRef?.Attribute("Version")?.Value;
        }
        catch
        {
            return null;
        }
    }

    private List<string> FindPackageDllsInCache(string packageName, string? version)
    {
        var dlls = new List<string>();
        var nugetCachePaths = GetNuGetCachePaths();

        foreach (var cachePath in nugetCachePaths)
        {
            if (!Directory.Exists(cachePath))
            {
                continue;
            }

            string packagePath;
            
            if (!string.IsNullOrEmpty(version))
            {
                packagePath = Path.Combine(cachePath, packageName.ToLowerInvariant(), version);
            }
            else
            {
                var packageDir = Path.Combine(cachePath, packageName.ToLowerInvariant());
                if (!Directory.Exists(packageDir))
                {
                    continue;
                }

                var versions = Directory.GetDirectories(packageDir)
                    .OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();

                if (versions == null)
                {
                    continue;
                }

                packagePath = versions;
            }

            if (!Directory.Exists(packagePath))
            {
                continue;
            }

            var dllFiles = Directory.GetFiles(packagePath, "*.dll", SearchOption.AllDirectories)
                .Where(f => !f.Contains("ref", StringComparison.OrdinalIgnoreCase))
                .Where(f => Path.GetFileNameWithoutExtension(f)
                    .StartsWith(packageName, StringComparison.OrdinalIgnoreCase));

            dlls.AddRange(dllFiles);
        }

        return dlls.Distinct().ToList();
    }

    private string[] GetNuGetCachePaths()
    {
        var paths = new List<string>();

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        
        paths.Add(Path.Combine(userProfile, ".nuget", "packages"));

        var nugetPackages = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrEmpty(nugetPackages))
        {
            paths.Add(nugetPackages);
        }

        return paths.ToArray();
    }

    private async Task<(int ExitCode, string Output)> RunDotnetCommandAsync(string arguments, string workingDirectory)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet", arguments)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        var output = new System.Text.StringBuilder();

        process.OutputDataReceived += (s, e) =>
        {
            if (e.Data != null)
            {
                output.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (s, e) =>
        {
            if (e.Data != null)
            {
                output.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        return (process.ExitCode, output.ToString());
    }
}

public class CommandException : Exception
{
    public CommandException(string message) : base(message)
    {
    }
}
