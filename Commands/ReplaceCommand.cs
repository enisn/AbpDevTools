using AbpDevTools.Configuration;
using CliFx.Infrastructure;
using Spectre.Console;
using System.Text.RegularExpressions;

namespace AbpDevTools.Commands;

[Command("replace", Description = "Runs file replacement according to configuration.")]
public class ReplaceCommand : ICommand
{
    [CommandOption("path", 'p', Description = "Working directory of the command. Probably solution directory. Default: . (CurrentDirectory) ")]
    public string WorkingDirectory { get; set; }

    [CommandParameter(0, IsRequired = false, Description = "If you execute single option from config, you can pass the name or pass 'all' to execute all of them")]
    public string ReplacementConfigName { get; set; }

    [CommandOption("interactive", 'i', Description = "Interactive Mode. It'll ask prompt to pick one config.")]
    public bool InteractiveMode { get; set; }

    private IConsole console;
    public async ValueTask ExecuteAsync(IConsole console)
    {
        this.console = console;
        WorkingDirectory ??= Directory.GetCurrentDirectory();

        var options = ReplacementConfiguration.GetOptions();

        if (string.IsNullOrEmpty(ReplacementConfigName))
        {
            if (InteractiveMode)
            {
                await console.Output.WriteLineAsync($"\n");
                ReplacementConfigName = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Choose a [blueviolet]rule[/] to execute?")
                        .PageSize(12)
                        .HighlightStyle(new Style(foreground: Color.BlueViolet))
                        .MoreChoicesText("[grey](Move up and down to reveal more rules)[/]")
                        .AddChoices(options.Keys));
            }
            else
            {
                console.Output.WriteLine("You should specify a execution rule name.\n");
                console.Output.WriteLine("\tUse 'replace <config-name>' to execute a rule");
                console.Output.WriteLine("\tUse 'replace all' to execute a rules");
                console.Output.WriteLine("\tUse 'replace config' to manage rules.\n\n");
                console.Output.WriteLine("Available execution rules:\n\n\t" + string.Join("\n\t -", options.Keys));
                return;
            }
        }

        if (ReplacementConfigName.Equals("all", StringComparison.InvariantCultureIgnoreCase))
        {
            foreach (var item in options)
            {
                await ExecuteConfigAsync(item.Key, item.Value);
            }
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
            await ExecuteConfigAsync(ReplacementConfigName, option);
            return;
        }
    }

    protected virtual async ValueTask ExecuteConfigAsync(string configurationName, ReplacementOption option)
    {
        await AnsiConsole.Status()
        .StartAsync($"Executing...", async ctx =>
        {
            AnsiConsole.MarkupLine($"Executing [blue]'{configurationName}'[/] replacement configuration...");

            ctx.Status($"[blue]{option.FilePattern}[/] file pattern executing.");
            var files = Directory.EnumerateFiles(WorkingDirectory, "*.*", SearchOption.AllDirectories)
                .Where(x => Regex.IsMatch(x, option.FilePattern))
                .ToList();

            await Task.Delay(2000);
            ctx.Status($"[green]{files.Count}[/] file(s) found with pattern.");

            int affectedFileCount = 0;
            foreach (var file in files)
            {
                var text = File.ReadAllText(file);

                if (text.Contains(option.Find))
                {
                    File.WriteAllText(file, text.Replace(option.Find, option.Replace));
                    await Task.Delay(2000);
                    ctx.Status($"{file} updated.");
                    affectedFileCount++;
                }
            }
            AnsiConsole.MarkupLine($"Totally [green]{affectedFileCount}[/] files updated.");
        });

    }
}
