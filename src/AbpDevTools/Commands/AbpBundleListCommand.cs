using System.Text;
using CliFx.Infrastructure;
using System.Xml;
using Spectre.Console;

[Command("bundle list", Description = "List projects that needs to run 'abp bundle'.")]
public class AbpBundleListCommand : ICommand
{
    [CommandParameter(0, IsRequired = false, Description = "Working directory for the command. Probably project or solution directory path goes here. Default: . (Current Directory)")]
    public string? WorkingDirectory { get; set; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }

        var wasmCsprojs = await AnsiConsole.Status()
            .StartAsync("Searching for Blazor WASM projects...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                var wasmCsprojs = GetWasmProjects();
                foreach (var csproj in wasmCsprojs)
                {
                    AnsiConsole.MarkupLine($"- .{Path.DirectorySeparatorChar}{Path.GetRelativePath(WorkingDirectory!, csproj.DirectoryName ?? string.Empty)}");
                }
                return wasmCsprojs;
            });

        if (!wasmCsprojs.Any())
        {
            await console.Output.WriteLineAsync("No Blazor WASM projects found. No files to bundle.");
            return;
        }
    }

    public IEnumerable<FileInfo> GetWasmProjects(){
        return Directory.EnumerateFiles(WorkingDirectory!, "*.csproj", SearchOption.AllDirectories)
                    .Where(IsCsprojBlazorWasm)
                    .Select(x => new FileInfo(x));
    }

    private static bool IsCsprojBlazorWasm(string file)
    {
        using var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read);
        using var reader = XmlReader.Create(fileStream, new XmlReaderSettings 
        { 
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreWhitespace = true
        });

        try
        {
            // Look for the Project element
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "Project")
                {
                    var sdk = reader.GetAttribute("Sdk");
                    return sdk == "Microsoft.NET.Sdk.BlazorWebAssembly";
                }
            }

            return false;
        }
        catch (XmlException)
        {
            return false;
        }
    }
}