using AbpDevTools.Configuration;
using CliFx.Infrastructure;
using Spectre.Console;

namespace AbpDevTools.Commands;

[Command("tools")]
public class ToolsCommand : ICommand
{
    protected readonly ToolsConfiguration toolsConfiguration;

    public ToolsCommand(ToolsConfiguration toolsConfiguration)
    {
        this.toolsConfiguration = toolsConfiguration;
    }

    public ValueTask ExecuteAsync(IConsole console)
    {
        var tools = toolsConfiguration.GetOptions();

        console.Output.WriteLine("Available tools:\n");

        var table = new Table().Border(TableBorder.Rounded);

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
