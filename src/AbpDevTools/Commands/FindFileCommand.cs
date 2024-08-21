using CliFx.Infrastructure;

namespace AbpDevTools.Commands;

[Command("find-file", Description = "Finds the specified text in the solution.")]
public class FindFileCommand : ICommand
{
    protected FileExplorer FileExplorer { get; }

    [CommandOption("ascendant", 'a', Description = "Determined searching direction as 'Ascendant' or 'Descendants'.")]
    public bool Ascendant { get; set; } = false;

    [CommandParameter(0, Description = "Text to search.")]
    public string SearchTerm { get; set; } = string.Empty;

    [CommandParameter(1, Description = "Directory to search", IsRequired = false)]
    public string WorkingDirectory { get; set; } = string.Empty;

    public FindFileCommand(FileExplorer fileExplorer)
    {
        FileExplorer = fileExplorer;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (string.IsNullOrWhiteSpace(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }

        foreach (var file in Find())
        {
            await console.Output.WriteLineAsync(file);
        }
    }

    IEnumerable<string> Find()
    {
        if (Ascendant)
        {
            return FileExplorer.FindAscendants(WorkingDirectory, SearchTerm);
        }
        else
        {
            return FileExplorer.FindDescendants(WorkingDirectory, SearchTerm);
        }
    }
}
