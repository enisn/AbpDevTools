using AbpDevTools.Configuration;
using CliFx.Infrastructure;
using System.Text.RegularExpressions;

namespace AbpDevTools.Commands;

[Command("replace", Description = "Runs file replacement according to configuration.")]
public class ReplaceCommand : ICommand
{
    [CommandOption("path", 'p', Description = "Working directory of the command. Probably solution directory. Default: . (CurrentDirectory) ")]
    public string WorkingDirectory { get; set; }

    [CommandParameter(0, IsRequired = false, Description = "If you execute single option from config, you can pass the name. Otherwise all options in configuration will be executed.")]
    public string ReplacementConfigName { get; set; }

    private IConsole console;
    public async ValueTask ExecuteAsync(IConsole console)
    {
        this.console = console;
        WorkingDirectory ??= Directory.GetCurrentDirectory();

        var options = ReplacementConfiguration.GetOptions();

        if (!string.IsNullOrEmpty(ReplacementConfigName))
        {
            if (!options.TryGetValue(ReplacementConfigName, out var option))
            {
                console.ForegroundColor = ConsoleColor.Red;
                await console.Error.WriteLineAsync($"No replacement config found with name '{ReplacementConfigName}'");
                console.ResetColor();

                await console.Output.WriteLineAsync("Available configurations: "+ string.Join(',', options.Keys));
                await console.Output.WriteLineAsync("Check existing configurations with 'abpdev config' command.");
                return;
            }
            await console.Output.WriteLineAsync($"Executing '{ReplacementConfigName}' replacement configuration...");
            await ExecuteConfigAsync(option);
            return;
        }

        foreach (var item in options)
        {
            await ExecuteConfigAsync(item.Value);
        }
    }

    protected virtual async ValueTask ExecuteConfigAsync(ReplacementOption option)
    {
        await console.Output.WriteLineAsync($"{option.FilePattern} file pattern executing.");

        var files = Directory.EnumerateFiles(WorkingDirectory, "*.*", SearchOption.AllDirectories)
            .Where(x => Regex.IsMatch(x, option.FilePattern))
        .ToList();

        await console.Output.WriteLineAsync($"{files.Count} file(s) found with pattern.");

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);

            if (text.Contains(option.Find))
            {
                File.WriteAllText(file, text.Replace(option.Find, option.Replace));
                await console.Output.WriteLineAsync($"{file} updated.");
            }
        }
    }
}
