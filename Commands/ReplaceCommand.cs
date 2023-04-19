using AbpDevTools.Configuration;
using CliFx.Infrastructure;
using System.Text.RegularExpressions;

namespace AbpDevTools.Commands;

[Command("replace", Description = "Runs file replacement according to configuration.")]
public class ReplaceCommand : ICommand
{
    [CommandParameter(0, IsRequired = false)]
    public string WorkingDirectory { get; set; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        WorkingDirectory ??= Directory.GetCurrentDirectory();

        var options = ReplacementConfiguration.GetOptions();

        foreach (var item in options)
        {
            await console.Output.WriteLineAsync($"{item.FilePattern} file pattern executing.");

            var files = Directory.EnumerateFiles(WorkingDirectory, "*.*", SearchOption.AllDirectories)
                .Where(x => Regex.IsMatch(x, item.FilePattern))
                .ToList();

            await console.Output.WriteLineAsync($"{files.Count} file(s) found with pattern.");

            foreach (var file in files)
            {
                var text = File.ReadAllText(file);

                if (text.Contains(item.Find))
                {
                    File.WriteAllText(file, text.Replace(item.Find, item.Replace));
                    await console.Output.WriteLineAsync($"{file} updated.");
                }
            }
        }
    }
}
