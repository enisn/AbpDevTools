using AbpDevTools.Configuration;
using CliFx.Infrastructure;
using Spectre.Console;
using System.Text;
using System.Text.RegularExpressions;

namespace AbpDevTools.Commands;

[Command("replace", Description = "Runs file replacement according to configuration.")]
public class ReplaceCommand : ICommand
{
    [CommandOption("path", 'p', Description = "Working directory of the command. Probably solution directory. Default: . (CurrentDirectory) ")]
    public string? WorkingDirectory { get; set; }

    [CommandParameter(0, IsRequired = false, Description = "If you execute single option from config, you can pass the name or pass 'all' to execute all of them")]
    public string? ReplacementConfigName { get; set; }

    [CommandOption("interactive", 'i', Description = "Interactive Mode. It'll ask prompt to pick one config.")]
    public bool InteractiveMode { get; set; }

    [CommandOption("files", 'f', Description = "Specific file names to replace. If provided, only these files will be processed. Supports partial match.")]
    public string[]? Files { get; set; }

    private readonly ReplacementConfiguration replacementConfiguration;

    public ReplaceCommand(ReplacementConfiguration replacementConfiguration)
    {
        this.replacementConfiguration = replacementConfiguration;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        WorkingDirectory ??= Directory.GetCurrentDirectory();

        var options = replacementConfiguration.GetOptions();
        List<string>? filesToProcess = null;

        if (string.IsNullOrEmpty(ReplacementConfigName))
        {
            if (InteractiveMode)
            {
                await console.Output.WriteLineAsync($"\n");
                ReplacementConfigName = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Choose a [mediumpurple2]rule[/] to execute?")
                        .PageSize(12)
                        .HighlightStyle(new Style(foreground: Color.MediumPurple2))
                        .MoreChoicesText("[grey](Move up and down to reveal more rules)[/]")
                        .AddChoices(options.Keys));
            }
            else
            {
                console.Output.WriteLine("You should specify a execution rule name.\n");
                console.Output.WriteLine("\tUse 'replace <config-name>' to execute a rule");
                console.Output.WriteLine("\tUse 'replace all' to execute a rules");
                console.Output.WriteLine("\tUse 'replace config' to manage rules.\n\n");
                console.Output.WriteLine("Available execution rules:\n\n\t - " + string.Join("\n\t - ", options.Keys));
                return;
            }
        }

        // If 'all' and interactive, prompt for files once
        if (ReplacementConfigName.Equals("all", StringComparison.InvariantCultureIgnoreCase))
        {
            if (InteractiveMode && (Files == null || Files.Length == 0))
            {
                // Use the union of all files matching any config's pattern
                var allFiles = options.Values
                    .SelectMany(opt => GetFilesMatchingPattern(opt.FilePattern))
                    .Distinct()
                    .ToList();
                var relToFull = allFiles.ToDictionary(
                    f => Path.GetRelativePath(WorkingDirectory!, f),
                    f => f
                );
                var selectedRel = AnsiConsole.Prompt(
                    new Spectre.Console.MultiSelectionPrompt<string>()
                        .Title($"Select files to apply [blue]all[/] replacement configurations:")
                        .NotRequired()
                        .PageSize(10)
                        .MoreChoicesText("[grey](Move up and down to reveal more files)[/]")
                        .InstructionsText("[grey](Press [blue]<space>[/] to toggle a file, [green]<enter>[/] to accept)[/]")
                        .AddChoices(relToFull.Keys)
                );
                filesToProcess = selectedRel.Select(rel => relToFull[rel]).ToList();
            }
            // else: filesToProcess remains null (handled in ExecuteConfigAsync)

            foreach (var item in options)
            {
                await ExecuteConfigAsync(item.Key, item.Value, filesToProcess);
            }

            return;
        }

        if (!string.IsNullOrEmpty(ReplacementConfigName))
        {
            if (!options.TryGetValue(ReplacementConfigName, out var option))
            {
                console.ForegroundColor = ConsoleColor.Red;
                await console.Error.WriteLineAsync($"No replacement config found with name '{ReplacementConfigName}'");
                console.ResetColor();

                await console.Output.WriteLineAsync("Available configurations: " + string.Join(',', options.Keys));
                await console.Output.WriteLineAsync("Check existing configurations with 'abpdev config' command.");
                return;
            }
            await ExecuteConfigAsync(ReplacementConfigName, option, null);
            return;
        }
    }

    protected virtual async ValueTask ExecuteConfigAsync(string configurationName, ReplacementOption option, List<string>? preselectedFiles = null)
    {
        await AnsiConsole.Status()
        .StartAsync($"Executing...", async ctx =>
        {
            AnsiConsole.MarkupLine($"Executing [blue]'{configurationName}'[/] replacement configuration...");

            await Task.Yield();

            ctx.Status($"[blue]{option.FilePattern}[/] file pattern executing.");
            var allFiles = GetFilesMatchingPattern(option.FilePattern);

            List<string> filesToProcess = allFiles;

            // If preselectedFiles is provided, filter to those that match this config's pattern
            if (preselectedFiles != null)
            {
                filesToProcess = preselectedFiles.Where(f => allFiles.Contains(f)).ToList();
            }
            else if (Files != null && Files.Length > 0)
            {
                filesToProcess = allFiles.Where(f => Files.Any(name => f.Contains(name, StringComparison.OrdinalIgnoreCase))).ToList();
            }
            else if (InteractiveMode)
            {
                // Show relative paths in prompt, but keep mapping to full paths
                var relToFull = allFiles.ToDictionary(
                    f => Path.GetRelativePath(WorkingDirectory!, f),
                    f => f
                );
                var selectedRel = AnsiConsole.Prompt(
                    new MultiSelectionPrompt<string>()
                        .Title($"Select files to apply [blue]{configurationName}[/] replacement:")
                        .NotRequired()
                        .PageSize(10)
                        .MoreChoicesText("[grey](Move up and down to reveal more files)[/]")
                        .InstructionsText("[grey](Press [blue]<space>[/] to toggle a file, [green]<enter>[/] to accept)[/]")
                        .AddChoices(relToFull.Keys)
                );
                filesToProcess = selectedRel.Select(rel => relToFull[rel]).ToList();
            }
            // else: filesToProcess is allFiles

            ctx.Status($"[green]{filesToProcess.Count}[/] file(s) selected for processing.");

            var affectedFileCount = ProcessFiles(filesToProcess, option, file => ctx.Status($"{file} updated."));

            AnsiConsole.MarkupLine($"Totally [green]{affectedFileCount}[/] files updated.");
        });

    }

    protected virtual List<string> GetFilesMatchingPattern(string filePattern)
    {
        return Directory.EnumerateFiles(WorkingDirectory!, "*.*", SearchOption.AllDirectories)
            .Where(filePath => MatchesFilePattern(filePath, filePattern))
            .ToList();
    }

    protected virtual int ProcessFiles(IEnumerable<string> filesToProcess, ReplacementOption option, Action<string>? onFileUpdated = null)
    {
        int affectedFileCount = 0;

        foreach (var file in filesToProcess)
        {
            var text = File.ReadAllText(file);

            if (!text.Contains(option.Find))
            {
                continue;
            }

            File.WriteAllText(file, text.Replace(option.Find, option.Replace));
            onFileUpdated?.Invoke(file);
            affectedFileCount++;
        }

        return affectedFileCount;
    }

    protected virtual bool MatchesFilePattern(string filePath, string filePattern)
    {
        if (LooksLikeRegex(filePattern))
        {
            var relativePath = NormalizePath(Path.GetRelativePath(WorkingDirectory!, filePath));
            return Regex.IsMatch(relativePath, filePattern)
                || Regex.IsMatch(Path.GetFileName(filePath), filePattern)
                || Regex.IsMatch(filePath, filePattern);
        }

        var normalizedPattern = NormalizePath(filePattern);
        var normalizedRelativePath = NormalizePath(Path.GetRelativePath(WorkingDirectory!, filePath));
        var target = normalizedPattern.Contains('/')
            ? normalizedRelativePath
            : Path.GetFileName(filePath);

        return Regex.IsMatch(target, ConvertGlobToRegex(normalizedPattern), RegexOptions.IgnoreCase);
    }

    private static bool LooksLikeRegex(string filePattern)
    {
        return filePattern.IndexOf('\\') >= 0
            || filePattern.IndexOfAny(['^', '$', '+', '|', '(', ')', '[', ']']) >= 0;
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string ConvertGlobToRegex(string pattern)
    {
        var regex = new StringBuilder("^");

        for (var i = 0; i < pattern.Length; i++)
        {
            var character = pattern[i];

            switch (character)
            {
                case '*':
                    if (i + 1 < pattern.Length && pattern[i + 1] == '*')
                    {
                        var isDirectoryWildcard = i + 2 < pattern.Length && pattern[i + 2] == '/';

                        if (isDirectoryWildcard)
                        {
                            regex.Append("(?:.*/)?");
                            i += 2;
                        }
                        else
                        {
                            regex.Append(".*");
                            i++;
                        }
                    }
                    else
                    {
                        regex.Append("[^/]*");
                    }
                    break;
                case '?':
                    regex.Append("[^/]");
                    break;
                case '{':
                    var closingBraceIndex = pattern.IndexOf('}', i + 1);

                    if (closingBraceIndex > i)
                    {
                        var options = pattern[(i + 1)..closingBraceIndex]
                            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                            .Select(Regex.Escape);

                        regex.Append("(?:");
                        regex.Append(string.Join('|', options));
                        regex.Append(')');
                        i = closingBraceIndex;
                    }
                    else
                    {
                        regex.Append("\\{");
                    }
                    break;
                default:
                    regex.Append(Regex.Escape(character.ToString()));
                    break;
            }
        }

        regex.Append('$');
        return regex.ToString();
    }
}
