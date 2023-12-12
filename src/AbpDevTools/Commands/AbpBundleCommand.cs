using CliFx.Infrastructure;
using Spectre.Console;
using System.Diagnostics;
using System.Text;

namespace AbpDevTools.Commands;

[Command("bundle", Description = "Runs 'abp bundle' command for each Blazor WASM projects recursively.")]
public class AbpBundleCommand : ICommand
{
    [CommandParameter(0, IsRequired = false, Description = "Working directory to run build. Probably project or solution directory path goes here. Default: . (Current Directory)")]
    public string? WorkingDirectory { get; set; }

    protected IConsole? console;

    public async ValueTask ExecuteAsync(IConsole console)
    {
        this.console = console;
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }

        console.RegisterCancellationHandler().Register(() =>
        {
            console.Output.WriteLine("Abp bundle cancelled.");
            throw new OperationCanceledException("Abp bundle cancelled.");
        });

        var wasmCsprojs = await AnsiConsole.Status()
            .StartAsync("Looking for projects", async ctx =>
            {
                ctx.Spinner(Spinner.Known.SimpleDotsScrolling);

                await Task.Yield();

                return Directory.EnumerateFiles(WorkingDirectory, "*.csproj", SearchOption.AllDirectories)
                    .Where(IsCsprojBlazorWasm)
                    .Select(x => new FileInfo(x))
                    .ToArray();
            });

        if (wasmCsprojs.Length == 0)
        {
            await console.Output.WriteLineAsync("No Blazor WASM projects found. No files to bundle.");

            return;
        }

        AnsiConsole.MarkupLine($"[green]{wasmCsprojs.Length}[/] blazor wasm projects found.");

        foreach (var csproj in wasmCsprojs)
        {
            await AnsiConsole.Status().StartAsync($"Running 'abp bundle' for {csproj.Name}...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.SimpleDotsScrolling);

                var startInfo = new ProcessStartInfo("abp", $"bundle -wd {Path.GetDirectoryName(csproj.FullName)}");
                startInfo.RedirectStandardOutput = true;
                using var process = Process.Start(startInfo)!;
                process.BeginOutputReadLine();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    AnsiConsole.MarkupLine($"[green]Success[/] while running 'abp bundle' for {csproj.Name}.");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Error[/] while running 'abp bundle' for {csproj.Name}.");
                }
            });
        }
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
