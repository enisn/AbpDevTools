using AbpDevTools.Configuration;
using AbpDevTools.Notifications;
using CliFx.Infrastructure;
using Spectre.Console;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace AbpDevTools.Commands;

[Command("abp-studio switch", Description = "Switches to ABP Studio version")]
public class AbpStudioSwitchCommand : ICommand
{
    [CommandParameter(0, IsRequired = true, Description = "Version of ABP Studio to install. Default: 0.7.6")]
    public string Version { get; set; } = "1.0.0";

    [CommandOption("channel", 'c', Description = "Channel of ABP Studio to install. Default: beta")]
    public string Channel { get; set; } = "stable";

    [CommandOption("force", 'f', Description = "Force download and install the package")]
    public bool Force { get; set; } = false;

    [CommandOption("install-dir", 'i', Description = "Path to the ABP Studio installation directory. Default: %localappdata%\\abp-studio")]
    public string? InstallDir { get; set; }

    [CommandOption("packages-dir", 'p', Description = "Path to the ABP Studio packages directory. You can use different folder for caching packages. Using custom folder provides fast version switching.Default: %localappdata%\\abp-studio\\packages")]
    public string? PackagesDir { get; set; }

    protected readonly INotificationManager notificationManager;
    protected readonly ToolsConfiguration toolsConfiguration;

    public AbpStudioSwitchCommand(INotificationManager notificationManager, ToolsConfiguration toolsConfiguration)
    {
        this.notificationManager = notificationManager;
        this.toolsConfiguration = toolsConfiguration;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        var installDir = GetInstallDir();
        var packagesDir = GetPackagesDir();

        AnsiConsole.MarkupLine("----------------------------------------");
        AnsiConsole.MarkupLine($"[blue]Switching to ABP Studio version {Version} on channel {Channel}...[/]");
        AnsiConsole.MarkupLine("----------------------------------------");
        AnsiConsole.MarkupLine($"[dim]Installing to {InstallDir}[/]");

        // Ensure directories exist
        if (!Directory.Exists(packagesDir))
        {
            Directory.CreateDirectory(packagesDir);
        }

        var fileName = $"abp-studio-{Version}-{Channel}-full.nupkg";
        var outputPath = Path.Combine(packagesDir, fileName);
        var url = $"https://abp.io/api/abp-studio/download/r/{OperatingSystemAlias}/{fileName}";
        await TryDownloadAsync(fileName, outputPath, url);

        AnsiConsole.MarkupLine("----------------------------------------");

        var updateExePath = GetUpdateExecutablePath();

        if (!File.Exists(updateExePath))
        {
            AnsiConsole.MarkupLine($"[red]Error: Update.exe not found at {updateExePath}[/]");
            AnsiConsole.MarkupLine("[red]ABP Studio may not be installed or the installation is corrupted.[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[dim]Running {updateExePath} apply --package {outputPath}[/]");

        var startInfo = new ProcessStartInfo(updateExePath, $"apply --package {outputPath}")
        {
            WorkingDirectory = InstallDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)!;

        process.OutputDataReceived += (sender, args) =>
        {
            if (args?.Data != null)
            {
                var escapedData = args.Data.Replace("[", "[[").Replace("]", "]]");
                AnsiConsole.MarkupLine($"[dim]{escapedData}[/]");
            }
        };

        process.ErrorDataReceived += (sender, args) =>
        {
            if (args?.Data != null)
            {
                var escapedData = args.Data.Replace("[", "[[").Replace("]", "]]");
                AnsiConsole.MarkupLine($"[red]{escapedData}[/]");
            }
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
        {
            AnsiConsole.MarkupLine("----------------------------------------");
            AnsiConsole.MarkupLine($"[green]ABP Studio version {Version} on channel {Channel} installed successfully.[/]");
            AnsiConsole.MarkupLine("----------------------------------------");

            await notificationManager.SendAsync(
                "ABP Studio Updated",
                $"Successfully switched to ABP Studio version {Version} on channel {Channel}");
        }
        else
        {
            AnsiConsole.MarkupLine("----------------------------------------");
            AnsiConsole.MarkupLine($"[red]Failed to install ABP Studio version {Version} on channel {Channel}.[/]");
            AnsiConsole.MarkupLine($"[red]Exit code: {process.ExitCode}[/]");
            AnsiConsole.MarkupLine("----------------------------------------");

            await notificationManager.SendAsync(
                "ABP Studio Update Failed",
                $"Failed to switch to ABP Studio version {Version} on channel {Channel}. Exit code: {process.ExitCode}");
        }
    }

    private async Task TryDownloadAsync(string fileName, string outputPath, string url)
    {
        if (Force && File.Exists(outputPath))
        {
            AnsiConsole.MarkupLine($"[yellow]Deleting existing file {outputPath} since force is enabled.[/]");
            File.Delete(outputPath);
        }

        // Check if file already exists
        if (File.Exists(outputPath))
        {
            AnsiConsole.MarkupLine($"[yellow]File {fileName} already exists. Skipping download.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[dim]Starting download from {url}[/]");

            await AnsiConsole.Status()
                .StartAsync($"Downloading {fileName}...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);

                    using var httpClient = new HttpClient();
                    using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException($"Failed to download {url}. Status: {response.StatusCode}");
                    }

                    var totalBytes = response.Content.Headers.ContentLength ?? -1;
                    var downloadedBytes = 0L;
                    var buffer = new byte[8192];

                    using var contentStream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = File.Create(outputPath);

                    int bytesRead;
                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        downloadedBytes += bytesRead;

                        if (totalBytes > 0)
                        {
                            var progressPercentage = (int)((double)downloadedBytes / totalBytes * 100);
                            var downloadedMB = downloadedBytes / (1024.0 * 1024.0);
                            var totalMB = totalBytes / (1024.0 * 1024.0);
                            
                            ctx.Status($"Downloading {fileName}... {progressPercentage}% ({downloadedMB:F1}MB / {totalMB:F1}MB)");
                        }
                        else
                        {
                            var downloadedMB = downloadedBytes / (1024.0 * 1024.0);
                            ctx.Status($"Downloading {fileName}... {downloadedMB:F1}MB downloaded");
                        }
                    }
                });

            AnsiConsole.MarkupLine($"[green]Downloaded {fileName} successfully.[/]");
        }
    }

    protected virtual string GetInstallDir()
    {
        if (InstallDir != null)
        {
            return InstallDir;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "/Applications/ABP Studio.app";
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "abp-studio");
    }

    protected virtual string GetPackagesDir()
    {
        if (PackagesDir != null)
        {
            return PackagesDir;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "abp-studio", "packages");
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "abp-studio", "packages");
    }

    protected virtual string GetUpdateExecutablePath()
    {
        var installDir = GetInstallDir();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Path.Combine(installDir, "Contents", "MacOS", "Update");
        }

        return Path.Combine(installDir, "Update.exe");
    }

    public static string OperatingSystemAlias
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                if (IsArmArchitecture)
                {
                    return "windows-arm";
                }
                return "windows";
            }
            else if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst())
            {
                if (!IsArmArchitecture)
                {
                    return "osx-intel";
                }
                return "osx";
            }
            else if (OperatingSystem.IsLinux())
            {
                return "linux";
            }

            return string.Empty;
        }
    }

    public static bool IsArmArchitecture
    {
        get
        {
            var arch = RuntimeInformation.OSArchitecture;
            return arch == Architecture.Arm64
                || arch == Architecture.Arm
#if NET7_0_OR_GREATER
                || arch == Architecture.Armv6
#endif
                ;
        }
    }
} 