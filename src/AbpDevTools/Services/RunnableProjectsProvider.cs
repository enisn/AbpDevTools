using AbpDevTools.Configuration;
using System.Text.Json;

namespace AbpDevTools.Services;

[RegisterSingleton]
public class RunnableProjectsProvider
{
    private static readonly string[] ExcludedNpmDirectories =
    {
        ".git",
        ".hg",
        ".svn",
        ".next",
        ".nuxt",
        "bin",
        "build",
        "coverage",
        "dist",
        "node_modules",
        "obj",
        "out"
    };

    public RunnableProjectsProvider(RunConfiguration runConfiguration)
    {
        runConfiguration.CleanObsolete();
    }

    public RunnableAppInfo[] GetRunnableApplications(string path, string[]? npmScripts = null)
    {
        return GetRunnableProjects(path)
            .Select(RunnableAppInfo.FromDotNetProject)
            .Concat(GetRunnableNpmProjects(path, npmScripts))
            .OrderBy(x => x.FullName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public FileInfo[] GetRunnableProjects(string path)
    {
        return Directory.EnumerateFiles(path, "*.csproj", SearchOption.AllDirectories)
            .Where(DoesHaveProgramClass)
            .Select(x => new FileInfo(x))
            .ToArray();
    }

    public FileInfo[] GetRunnableProjectsWithMigrateDatabaseParameter(string path)
    {
        return Directory.EnumerateFiles(path, "*.csproj", SearchOption.AllDirectories)
            .Where(DoesHaveProgramClass)
            .Where(DoesHaveMigrateDatabaseParameter)
            .Select(x => new FileInfo(x))
            .ToArray();
    }

    public RunnableAppInfo[] GetRunnableNpmProjects(string path, string[]? configuredScripts = null)
    {
        if (!Directory.Exists(path))
        {
            return Array.Empty<RunnableAppInfo>();
        }

        return EnumeratePackageJsonFiles(path)
            .Select(packageJsonPath => TryCreateNpmRunnableApp(packageJsonPath, configuredScripts))
            .Where(x => x != null)
            .Cast<RunnableAppInfo>()
            .OrderBy(x => x.FullName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static RunnableAppInfo? TryCreateNpmRunnableApp(string packageJsonPath, string[]? configuredScripts)
    {
        try
        {
            using var packageJson = JsonDocument.Parse(
                File.ReadAllText(packageJsonPath),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });

            if (!packageJson.RootElement.TryGetProperty("scripts", out var scripts) ||
                scripts.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var selectedScript = SelectRunnableNpmScript(scripts, configuredScripts);
            if (selectedScript == null)
            {
                return null;
            }

            var packageDirectory = Path.GetDirectoryName(packageJsonPath)!;
            var packageName = TryGetStringProperty(packageJson.RootElement, "name") ?? Path.GetFileName(packageDirectory);

            return new RunnableAppInfo
            {
                Type = RunnableAppType.Npm,
                Name = $"{packageName}:{selectedScript.Name}",
                FullName = packageJsonPath,
                WorkingDirectory = packageDirectory,
                Script = selectedScript.Name,
                PackageManager = DetectPackageManager(packageJson.RootElement),
                IsRunByDefault = selectedScript.IsRunByDefault
            };
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static NpmScriptSelection? SelectRunnableNpmScript(JsonElement scripts, string[]? configuredScripts)
    {
        foreach (var configuredScript in configuredScripts ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(configuredScript))
            {
                continue;
            }

            if (TryGetScriptCommand(scripts, configuredScript, out _))
            {
                return new NpmScriptSelection(configuredScript, IsRunByDefault: true);
            }
        }

        if (TryGetScriptCommand(scripts, "dev", out _))
        {
            return new NpmScriptSelection("dev", IsRunByDefault: true);
        }

        if (TryGetScriptCommand(scripts, "serve", out _))
        {
            return new NpmScriptSelection("serve", IsRunByDefault: true);
        }

        if (TryGetScriptCommand(scripts, "start", out var startCommand))
        {
            return new NpmScriptSelection("start", IsKnownWebServerScript(startCommand));
        }

        return null;
    }

    private static bool TryGetScriptCommand(JsonElement scripts, string scriptName, out string command)
    {
        command = string.Empty;

        if (!scripts.TryGetProperty(scriptName, out var script) || script.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        command = script.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(command);
    }

    private static bool IsKnownWebServerScript(string command)
    {
        var normalizedCommand = command.ToLowerInvariant();

        return normalizedCommand.Contains("ng serve") ||
               normalizedCommand.Contains("react-scripts start") ||
               normalizedCommand.Contains("vue-cli-service serve") ||
               normalizedCommand.Contains("next dev") ||
               normalizedCommand.Contains("nuxt dev") ||
               normalizedCommand.Contains("astro dev") ||
               normalizedCommand.Contains("svelte-kit dev") ||
               normalizedCommand.Contains("webpack serve") ||
               normalizedCommand.Contains("remix dev") ||
               ContainsCommandToken(normalizedCommand, "vite");
    }

    private static bool ContainsCommandToken(string command, string token)
    {
        return command.Equals(token, StringComparison.Ordinal) ||
               command.StartsWith(token + " ", StringComparison.Ordinal) ||
               command.EndsWith(" " + token, StringComparison.Ordinal) ||
               command.Contains(" " + token + " ", StringComparison.Ordinal) ||
               command.Contains(" " + token + " --", StringComparison.Ordinal);
    }

    private static string DetectPackageManager(JsonElement packageJson)
    {
        var packageManager = TryGetStringProperty(packageJson, "packageManager");
        if (!string.IsNullOrWhiteSpace(packageManager))
        {
            var name = packageManager.Split('@', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (IsSupportedPackageManager(name))
            {
                return name!;
            }
        }

        return "npm";
    }

    private static bool IsSupportedPackageManager(string? packageManager)
    {
        return packageManager is "npm" or "pnpm" or "yarn" or "bun";
    }

    private static string? TryGetStringProperty(JsonElement jsonElement, string propertyName)
    {
        if (!jsonElement.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = property.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static IEnumerable<string> EnumeratePackageJsonFiles(string path)
    {
        var directories = new Stack<string>();
        directories.Push(path);

        while (directories.Count > 0)
        {
            var currentDirectory = directories.Pop();
            var packageJsonPath = Path.Combine(currentDirectory, "package.json");

            if (File.Exists(packageJsonPath))
            {
                yield return packageJsonPath;
            }

            IEnumerable<string> childDirectories;
            try
            {
                childDirectories = Directory.EnumerateDirectories(currentDirectory).ToArray();
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var childDirectory in childDirectories)
            {
                if (!IsExcludedNpmDirectory(childDirectory))
                {
                    directories.Push(childDirectory);
                }
            }
        }
    }

    private static bool IsExcludedNpmDirectory(string directory)
    {
        var directoryName = Path.GetFileName(directory);
        if (ExcludedNpmDirectories.Any(x => string.Equals(x, directoryName, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var parentDirectory = Path.GetFileName(Path.GetDirectoryName(directory));
        return string.Equals(directoryName, "libs", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(parentDirectory, "wwwroot", StringComparison.OrdinalIgnoreCase);
    }

    private static bool DoesHaveProgramClass(string projectPath)
    {
        return File.Exists(
            Path.Combine(
                Path.GetDirectoryName(projectPath)!,
                "Program.cs"
            ));
    }

    public bool DoesHaveMigrateDatabaseParameter(string projectPath)
    {
        if (!File.Exists(projectPath))
        {
            return false;
        }
        
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        
        // Check Program.cs first
        var programCsFilePath = Path.Combine(projectDirectory, "Program.cs");
        if (File.Exists(programCsFilePath) && ContainsMigrateDatabaseText(programCsFilePath))
        {
            return true;
        }
        
        // Check all *Module.cs files in the project directory
        var moduleFiles = Directory.EnumerateFiles(projectDirectory, "*Module.cs", SearchOption.TopDirectoryOnly);
        foreach (var moduleFile in moduleFiles)
        {
            if (ContainsMigrateDatabaseText(moduleFile))
            {
                return true;
            }
        }
        
        return false;
    }
    
    private static bool ContainsMigrateDatabaseText(string filePath)
    {
        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new StreamReader(fileStream);
            
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Contains("--migrate-database", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            
            return false;
        }
        catch (IOException)
        {
            // If we can't read the file, assume it doesn't contain the text
            return false;
        }
    }
}

public enum RunnableAppType
{
    DotNet,
    Npm
}

public class RunnableAppInfo
{
    public RunnableAppType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string? Script { get; set; }
    public string? PackageManager { get; set; }
    public bool IsRunByDefault { get; set; } = true;

    public static RunnableAppInfo FromDotNetProject(FileInfo projectFile)
    {
        return new RunnableAppInfo
        {
            Type = RunnableAppType.DotNet,
            Name = projectFile.Name,
            FullName = projectFile.FullName,
            WorkingDirectory = projectFile.DirectoryName!,
            IsRunByDefault = true
        };
    }
}

internal sealed record NpmScriptSelection(string Name, bool IsRunByDefault);
