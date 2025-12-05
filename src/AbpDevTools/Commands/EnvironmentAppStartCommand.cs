using AbpDevTools.Configuration;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using Spectre.Console;
using System.Diagnostics;
using System.Text;

namespace AbpDevTools.Commands;
[Command("envapp start", Description = "Deploys infrastructural tools to docker. Such as Redis, RabbitMQ, SqlServer etc.")]
public class EnvironmentAppStartCommand : ICommand
{
    [CommandParameter(0, IsRequired = false, Description = "Name of the app.")]
    public string[] AppNames { get; set; } = Array.Empty<string>();

    [CommandOption("password", 'p', Description = "Default password for sql images when applicable. Default: 12345678Aa")]
    public string DefaultPassword { get; set; } = "12345678Aa";

    [CommandOption("verbose", 'v', Description = "Show detailed output from Docker commands.")]
    public bool Verbose { get; set; }

    protected IConsole? console;
    protected Dictionary<string,EnvironmentToolOption> configurations;

    public EnvironmentAppStartCommand(EnvironmentAppConfiguration environmentAppConfiguration)
    {
        configurations = environmentAppConfiguration.GetOptions();
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        this.console = console;

        if (AppNames == null || AppNames.Length == 0)
        {
            console.Output.WriteLine("You must specify an app to run.\n" +
                "envapp start <ToolName>\n" +
                "Available app names:\n - " + string.Join("\n - ", configurations.Keys));

            return;
        }

        foreach (var appName in AppNames)
        {
            await StartAppAsync(appName);
        }
    }

    protected async Task StartAppAsync(string appName)
    {
        if (string.IsNullOrEmpty(appName))
        {
            await console!.Output.WriteAsync("App Name can't be null or empty.");
            return;
        }

        if (!configurations.TryGetValue(appName, out var option))
        {
            throw new CommandException($"ToolName '{appName}' couldn't be recognized. Try one of them: \n - " + string.Join("\n - ", configurations.Keys));
        }

        if (string.IsNullOrEmpty(DefaultPassword))
        {
            DefaultPassword = "12345678Aa";
        }

        var commands = option.StartCmds.Select(cmd => cmd.Replace("Passw0rd", DefaultPassword)).ToArray();
        await RunCommandsAsync(appName,commands, useOrLogic: true);
    }

    protected async Task RunCommandsAsync(string appName, string[] commands, bool useOrLogic)
    {
        for (int i = 0; i < commands.Length; i++)
        {
            var command = commands[i].Trim();
            if (string.IsNullOrEmpty(command))
            {
                continue;
            }

            var spaceIndex = command.IndexOf(' ');
            var fileName = spaceIndex > 0 ? command[..spaceIndex] : command;
            var arguments = spaceIndex > 0 ? command[spaceIndex..] : string.Empty;

            var processStartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (Verbose)
            {
                AnsiConsole.MarkupLine($"[dim]Executing: {command.EscapeMarkup()}[/]");
            }

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();
            var exitCode = -1;

            if (Verbose)
            {
                // In verbose mode, stream all output directly
                exitCode = await RunProcessAsync(
                    processStartInfo,
                    outputBuilder,
                    errorBuilder,
                    onStdOutLine: line => AnsiConsole.WriteLine(line),
                    onStdErrLine: line => AnsiConsole.MarkupLine($"[red]{line.EscapeMarkup()}[/]")
                );
            }
            else
            {
                // In non-verbose mode, show a single updating status line
                await AnsiConsole.Status()
                    .StartAsync("Starting app...", async ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Dots);
                        ctx.SpinnerStyle(Style.Parse("green"));

                        exitCode = await RunProcessAsync(
                            processStartInfo,
                            outputBuilder,
                            errorBuilder,
                            onStdOutLine: line =>
                            {
                                if (!string.IsNullOrWhiteSpace(line))
                                {
                                    ctx.Status = line.EscapeMarkup();
                                }
                            },
                            onStdErrLine: line =>
                            {
                                if (!string.IsNullOrWhiteSpace(line))
                                {
                                    ctx.Status = $"{line.EscapeMarkup()}";
                                }
                            });
                    });
            }

            var output = outputBuilder.ToString();
            var error = errorBuilder.ToString();

            var isLastCommand = i == commands.Length - 1;

            // If using OR logic, only continue to next command if this one failed
            if (useOrLogic && exitCode == 0)
            {
                // Always show success message
                AnsiConsole.MarkupLine($"[green]✓ App {appName} started successfully.[/]");
                
                // Only show Docker output in verbose mode
                if (Verbose)
                {
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        AnsiConsole.WriteLine(output);
                    }
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        AnsiConsole.WriteLine(error);
                    }
                }
                break;
            }
            
            // If this is the last command
            if (isLastCommand)
            {
                if (exitCode == 0)
                {
                    // Always show success message
                    AnsiConsole.MarkupLine("[green]✓ Container created/started successfully.[/]");
                    
                    // Only show Docker output in verbose mode
                    if (Verbose)
                    {
                        if (!string.IsNullOrWhiteSpace(output))
                        {
                            AnsiConsole.WriteLine(output);
                        }
                        if (!string.IsNullOrWhiteSpace(error))
                        {
                            AnsiConsole.WriteLine(error);
                        }
                    }
                }
                else
                {
                    // Always show error message and details
                    AnsiConsole.MarkupLine("[red]✗ Failed to start/create container.[/]");
                    
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        AnsiConsole.MarkupLine($"[red]{error.EscapeMarkup()}[/]");
                    }
                    
                    // Show stdout only in verbose mode
                    if (Verbose && !string.IsNullOrWhiteSpace(output))
                    {
                        AnsiConsole.WriteLine(output);
                    }
                }
            }
        }

        static async Task<int> RunProcessAsync(
            ProcessStartInfo startInfo,
            StringBuilder outputBuilder,
            StringBuilder errorBuilder,
            Action<string>? onStdOutLine,
            Action<string>? onStdErrLine)
        {
            using (var process = new Process())
            {
                process.StartInfo = startInfo;
                process.EnableRaisingEvents = true;

                var stdOutTcs = new TaskCompletionSource<bool>();
                var stdErrTcs = new TaskCompletionSource<bool>();

                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data is null)
                    {
                        stdOutTcs.TrySetResult(true);
                        return;
                    }

                    outputBuilder.AppendLine(e.Data);
                    onStdOutLine?.Invoke(e.Data);
                };

                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data is null)
                    {
                        stdErrTcs.TrySetResult(true);
                        return;
                    }

                    errorBuilder.AppendLine(e.Data);
                    onStdErrLine?.Invoke(e.Data);
                };

                if (!process.Start())
                {
                    throw new CommandException($"Failed to start process '{startInfo.FileName}'.");
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await Task.WhenAll(process.WaitForExitAsync(), stdOutTcs.Task, stdErrTcs.Task);

                return process.ExitCode;
            }
        }
    }
}
