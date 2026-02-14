using AbpDevTools.Configuration;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using System.Diagnostics;
using System.Text;

namespace AbpDevTools.Commands;

[Command("template", Description = "Template scaffolding commands.")]
public class TemplateCommand : ICommand
{
    public async ValueTask ExecuteAsync(IConsole console)
    {
        await console.Output.WriteLineAsync("Usage:");
        await console.Output.WriteLineAsync("  abpdev template list");
        await console.Output.WriteLineAsync("  abpdev template create dotnet <ProjectName> [-o <output>] [--module-class-name <Name>] [--abp-version <Version>] [--force]");
        await console.Output.WriteLineAsync("  abpdev template create npm <PackageName> [-o <output>] [--description <Text>] [--abp-version <Version>] [--force]");
    }
}

[Command("template list", Description = "Lists built-in templates.")]
public class TemplateListCommand : ICommand
{
    public async ValueTask ExecuteAsync(IConsole console)
    {
        await console.Output.WriteLineAsync("Built-in templates:");
        await console.Output.WriteLineAsync("  dotnet  - ABP module library template (abp-module-simple)");
        await console.Output.WriteLineAsync("  npm     - ABP-compatible npm package template (abp-package-simple)");
    }
}

[Command("template create", Description = "Creates a project from built-in templates.")]
public class TemplateCreateCommand : ICommand
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    [CommandParameter(0, Description = "Template type: dotnet or npm")]
    public string TemplateType { get; set; } = string.Empty;

    [CommandParameter(1, Description = "Project/package name")]
    public string Name { get; set; } = string.Empty;

    [CommandOption("output", 'o', Description = "Output directory. Default: ./<name>")]
    public string? Output { get; set; }

    [CommandOption("abp-version", Description = "ABP package version")]
    public string AbpVersion { get; set; } = "10.0.2";

    [CommandOption("module-class-name", Description = "ABP module class prefix for dotnet template")]
    public string? ModuleClassName { get; set; }

    [CommandOption("description", Description = "Description for npm package template")]
    public string Description { get; set; } = "ABP-compatible npm package";

    [CommandOption("force", 'f', Description = "Overwrite output directory if it exists and is not empty")]
    public bool Force { get; set; }

    private readonly ToolOption tools;

    public TemplateCreateCommand(ToolsConfiguration toolsConfiguration)
    {
        tools = toolsConfiguration.GetOptions();
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (string.IsNullOrWhiteSpace(TemplateType))
        {
            throw new CommandException("Template type is required. Use: dotnet or npm.");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new CommandException("Name is required.");
        }

        var outputPath = Path.GetFullPath(Output ?? Path.Combine(Directory.GetCurrentDirectory(), Name));
        PrepareOutputDirectory(outputPath, Force);

        var templatesRoot = ResolveTemplatesRoot();

        switch (TemplateType.Trim().ToLowerInvariant())
        {
            case "dotnet":
            case "dotnet-lib":
            case "dotnet-library":
                await CreateDotnetTemplateAsync(console, templatesRoot, outputPath);
                break;

            case "npm":
            case "npm-package":
                await CreateNpmTemplateAsync(console, templatesRoot, outputPath);
                break;

            default:
                throw new CommandException($"Unknown template type '{TemplateType}'. Use 'dotnet' or 'npm'.");
        }
    }

    private static void PrepareOutputDirectory(string outputPath, bool force)
    {
        if (!Directory.Exists(outputPath) || !Directory.EnumerateFileSystemEntries(outputPath).Any())
        {
            return;
        }

        if (!force)
        {
            throw new CommandException($"Output directory already exists and is not empty: {outputPath}");
        }

        ClearDirectory(outputPath);
    }

    private static void ClearDirectory(string outputPath)
    {
        foreach (var file in Directory.EnumerateFiles(outputPath))
        {
            File.Delete(file);
        }

        foreach (var directory in Directory.EnumerateDirectories(outputPath))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string ResolveTemplatesRoot()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "templates"),
            AppContext.BaseDirectory,
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "templates")),
            Path.Combine(Directory.GetCurrentDirectory(), "templates")
        };

        var existing = candidates.FirstOrDefault(IsTemplateRoot);
        if (existing is null)
        {
            throw new CommandException("Templates folder could not be found.");
        }

        return existing;
    }

    private static bool IsTemplateRoot(string path)
    {
        if (!Directory.Exists(path))
        {
            return false;
        }

        return Directory.Exists(Path.Combine(path, "dotnet", "abp-module-simple"))
            && Directory.Exists(Path.Combine(path, "npm", "abp-package-simple"));
    }

    private async Task CreateDotnetTemplateAsync(IConsole console, string templatesRoot, string outputPath)
    {
        var templatePath = Path.Combine(templatesRoot, "dotnet", "abp-module-simple");
        if (!Directory.Exists(templatePath))
        {
            throw new CommandException($"Dotnet template path not found: {templatePath}");
        }

        var moduleClassName = string.IsNullOrWhiteSpace(ModuleClassName)
            ? ToPascalIdentifier(Name.Split('.').Last())
            : ToPascalIdentifier(ModuleClassName!);

        await console.Output.WriteLineAsync($"Creating dotnet template at {outputPath}");

        var installArgs = $"new install \"{templatePath}\"";
        var createArgs = $"new abp-module-simple -n \"{Name}\" -o \"{outputPath}\" --ModuleClassName \"{moduleClassName}\" --AbpVersion \"{AbpVersion}\"";
        var uninstallArgs = $"new uninstall \"{templatePath}\"";

        try
        {
            await RunProcessAsync(tools["dotnet"], installArgs, Directory.GetCurrentDirectory());
            await RunProcessAsync(tools["dotnet"], createArgs, Directory.GetCurrentDirectory());
        }
        finally
        {
            try
            {
                await RunProcessAsync(tools["dotnet"], uninstallArgs, Directory.GetCurrentDirectory(), allowFail: true);
            }
            catch
            {
            }
        }

        await console.Output.WriteLineAsync("Dotnet template created successfully.");
    }

    private async Task CreateNpmTemplateAsync(IConsole console, string templatesRoot, string outputPath)
    {
        var templatePath = Path.Combine(templatesRoot, "npm", "abp-package-simple");
        if (!Directory.Exists(templatePath))
        {
            throw new CommandException($"Npm template path not found: {templatePath}");
        }

        await console.Output.WriteLineAsync($"Creating npm template at {outputPath}");

        CopyDirectory(templatePath, outputPath);

        var replacements = new Dictionary<string, string>
        {
            ["__PACKAGE_NAME__"] = Name,
            ["__ABP_VERSION__"] = AbpVersion,
            ["__DESCRIPTION__"] = Description
        };

        foreach (var file in Directory.EnumerateFiles(outputPath, "*", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (var replacement in replacements)
            {
                text = text.Replace(replacement.Key, replacement.Value, StringComparison.Ordinal);
            }

            File.WriteAllText(file, text, Utf8NoBom);
        }

        await console.Output.WriteLineAsync("Npm template created successfully.");
        await console.Output.WriteLineAsync("Reminder: 'abp.resourcemappings.js' is included as a safe stub.");
    }

    private static void CopyDirectory(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(destinationPath);

        foreach (var directory in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourcePath, directory);
            Directory.CreateDirectory(Path.Combine(destinationPath, relative));
        }

        foreach (var file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourcePath, file);
            var targetPath = Path.Combine(destinationPath, relative);
            var targetDirectory = Path.GetDirectoryName(targetPath)!;
            Directory.CreateDirectory(targetDirectory);
            File.Copy(file, targetPath, overwrite: false);
        }
    }

    private static string ToPascalIdentifier(string value)
    {
        var parts = value
            .Split(new[] { '.', '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(static part => char.ToUpperInvariant(part[0]) + part[1..]);

        var identifier = string.Concat(parts);
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return "MyModule";
        }

        if (!char.IsLetter(identifier[0]))
        {
            identifier = "M" + identifier;
        }

        return identifier;
    }

    private static async Task RunProcessAsync(string fileName, string arguments, string workingDirectory, bool allowFail = false)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo)
            ?? throw new CommandException($"Could not start process: {fileName}");

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0 && !allowFail)
        {
            throw new CommandException($"Command failed: {fileName} {arguments}\n{output}\n{error}");
        }
    }
}
