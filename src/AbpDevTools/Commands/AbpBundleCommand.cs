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

    [CommandOption("graphBuild", 'g', Description = "Graph builds project before running 'abp bundle'.")]
    public bool GraphBuild { get; set; }

    protected IConsole? console;
    protected AbpBundleListCommand listCommand;

    public AbpBundleCommand(AbpBundleListCommand listCommand)
    {
        this.listCommand = listCommand;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        this.console = console;
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }
        listCommand.WorkingDirectory = WorkingDirectory;

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

                return listCommand.GetWasmProjects().ToArray();
            });

        if (!wasmCsprojs.Any())
        {
            await console.Output.WriteLineAsync("No Blazor WASM projects found. No files to bundle.");

            return;
        }

        AnsiConsole.MarkupLine($"[green]{wasmCsprojs.Length}[/] blazor wasm projects found.");

        foreach (var csproj in wasmCsprojs)
        {
            if (GraphBuild)
            {
                var index = Array.IndexOf(wasmCsprojs, csproj) + 1;
                var compiled = await AnsiConsole.Status().StartAsync($"[grey]{index/wasmCsprojs.Length} Building {csproj.Name}...[/]", async (ctx) =>
                {
                    ctx.Spinner(Spinner.Known.SimpleDotsScrolling);

                    var startInfo = new ProcessStartInfo("dotnet", $"build /graphBuild")
                    {
                        WorkingDirectory = Path.GetDirectoryName(csproj.FullName)!,
                    };
                    startInfo.RedirectStandardOutput = true;
                    using var process = Process.Start(startInfo)!;
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        AnsiConsole.MarkupLine($"[green]Completed[/][grey] Building {csproj.Name}[/]");
                        return true;
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]Couldn't compile[/] {csproj.Name}");
                        return false;
                    }
                });

                if (!compiled)
                {
                    continue;
                }
            }

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
}
