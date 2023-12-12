using AbpDevTools.Configuration;
using CliFx.Infrastructure;
using Spectre.Console;

namespace AbpDevTools.Commands;

[Command("tools")]
public class ToolsCommand : ICommand
{
    public ValueTask ExecuteAsync(IConsole console)
    {
        var tools = ToolsConfiguration.GetOptions();

        console.Output.WriteLine("Available tools:\n");

        var table = new Table();

        table.AddColumn("Tool");
        table.AddColumn("Path");

        foreach (var tool in tools)
        {
            table.AddRow(tool.Key, tool.Value);
        }

        AnsiConsole.Write(table);

        console.Output.WriteLine("\nYou can change tools with the 'abpdev tools config' command.\n");
        return ValueTask.CompletedTask;
    }
}
