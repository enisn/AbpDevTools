using AbpDevTools.Configuration;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using Spectre.Console;
using System.Diagnostics;

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
        await RunCommandsAsync(commands, useOrLogic: true);
    }

    protected async Task RunCommandsAsync(string[] commands, bool useOrLogic)
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

            var process = Process.Start(processStartInfo);
            
            var outputTask = process!.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync();
            
            var output = await outputTask;
            var error = await errorTask;

            var isLastCommand = i == commands.Length - 1;

            // If using OR logic, only continue to next command if this one failed
            if (useOrLogic && process.ExitCode == 0)
            {
                // Always show success message
                AnsiConsole.MarkupLine("[green]✓ Container started successfully.[/]");
                
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
                if (process.ExitCode == 0)
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
    }
}
