using System.Text;
using CliFx.Infrastructure;

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

        var wasmCsprojs = GetWasmProjects();

        if (wasmCsprojs.Length == 0)
        {
            await console.Output.WriteLineAsync("No Blazor WASM projects found. No files to bundle.");

            return;
        }

        await console.Output.WriteLineAsync("Blazor WASM projects found:");
        foreach (var csproj in wasmCsprojs)
        {
            await console.Output.WriteLineAsync($"- {csproj.FullName}");
        }
    }

    public FileInfo[] GetWasmProjects(){
        return Directory.EnumerateFiles(WorkingDirectory!, "*.csproj", SearchOption.AllDirectories)
                    .Where(IsCsprojBlazorWasm)
                    .Select(x => new FileInfo(x))
                    .ToArray();
    }

    static bool IsCsprojBlazorWasm(string file)
    {
        using var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read);
        using var streamReader = new StreamReader(fileStream, Encoding.UTF8, true);

        for (int i = 0; i < 4; i++)
        {
            var line = streamReader.ReadLine();

            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            if (line.Contains("Sdk=\"Microsoft.NET.Sdk.BlazorWebAssembly\""))
            {
                return true;
            }
        }

        return false;
    }
}