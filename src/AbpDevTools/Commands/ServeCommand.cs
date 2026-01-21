using CliFx.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Spectre.Console;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;

namespace AbpDevTools.Commands;

[Command("serve", Description = "Starts a web server for browser-based UI")]
public class ServeCommand : ICommand
{
    [CommandOption("port", 'p', Description = "Port to listen on. Default: 5000")]
    public int Port { get; set; } = 5000;

    [CommandOption("no-open", 'n', Description = "Do not open browser automatically")]
    public bool NoOpen { get; set; }

    private WebApplication? _app;
    private CancellationTokenSource? _cts;

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var workingDirectory = Directory.GetCurrentDirectory();
        
        AnsiConsole.MarkupLine($"[green]Starting web server on port {Port}...[/]");
        
        _cts = new CancellationTokenSource();
        var cancellationToken = console.RegisterCancellationHandler();
        
        // Link cancellation to our token source
        cancellationToken.Register(() => _cts?.Cancel());

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot"),
            Args = Array.Empty<string>()
        });

        _app = builder.Build();

        // Serve static files
        _app.UseFileServer(new FileServerOptions
        {
            EnableDefaultFiles = true,
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
                Path.Combine(AppContext.BaseDirectory, "wwwroot"))
        });

        // API endpoint: List projects
        _app.MapGet("/api/projects", (HttpContext context) =>
        {
            var projects = new List<object>();
            
            // Find .sln files
            var solutions = Directory.EnumerateFiles(workingDirectory, "*.sln", SearchOption.TopDirectoryOnly)
                .Select(f => new 
                {
                    Name = Path.GetFileName(f),
                    Path = f,
                    Type = "solution"
                });

            projects.AddRange(solutions);

            // Find .csproj files
            var projFiles = Directory.EnumerateFiles(workingDirectory, "*.csproj", SearchOption.AllDirectories)
                .Select(f => new 
                {
                    Name = Path.GetFileName(f),
                    Path = f,
                    Type = "project"
                });

            projects.AddRange(projFiles);

            return Results.Ok(projects);
        });

        _app.MapFallbackToFile("/index.html");

        try
        {
            var url = $"http://localhost:{Port}";
            
            // Start the server
            var runTask = _app.RunAsync(url);
            
            AnsiConsole.MarkupLine($"[green]✓[/] Web server running at [link]{url}[/]");
            AnsiConsole.MarkupLine($"[grey]Press Ctrl+C to stop[/]");

            // Auto-open browser
            if (!NoOpen)
            {
                try
                {
                    var browserCmd = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd /c start" :
                                     RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "open" : "xdg-open";
                    
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = browserCmd,
                        Arguments = url,
                        CreateNoWindow = true,
                        UseShellExecute = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    });
                }
                catch
                {
                    // Ignore browser open errors
                }
            }

            // Wait for cancellation
            await Task.Delay(Timeout.Infinite, _cts.Token);
        }
        catch (TaskCanceledException)
        {
            AnsiConsole.MarkupLine("\n[yellow]Stopping web server...[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
        }
        finally
        {
            if (_app != null)
            {
                await _app.StopAsync();
                await _app.DisposeAsync();
            }
            _cts?.Dispose();
            AnsiConsole.MarkupLine("[green]✓[/] Web server stopped");
        }
    }
}
