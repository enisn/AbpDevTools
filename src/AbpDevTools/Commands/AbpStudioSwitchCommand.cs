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
    [CommandParameter(0, IsRequired = true, Description = "Version of ABP Studio to install")]
    public string Version { get; set; } = "1.0.0";

    [CommandOption("channel", 'c', Description = "Channel of ABP Studio to install")]
    public string Channel { get; set; } = "stable";

    [CommandOption("force", 'f', Description = "Force download and install the package")]
    public bool Force { get; set; } = false;

    [CommandOption("install-dir", 'i', Description = "Path to the ABP Studio installation directory. On Linux, this can be the AppImage file or its containing directory.")]
    public string? InstallDir { get; set; }

    [CommandOption("packages-dir", 'p', Description = "Path to the ABP Studio packages directory. A custom cache enables faster version switching.")]
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
        var platform = GetCurrentPlatform();
        var installPath = platform == AbpStudioPlatform.Linux
            ? LinuxAbpStudioInstaller.ResolveInstalledAppImage(InstallDir)
            : GetInstallDir();
        var architecture = platform == AbpStudioPlatform.Linux
            ? LinuxAbpStudioInstaller.ReadAppImageArchitecture(installPath)
            : RuntimeInformation.OSArchitecture;
        var release = ResolveReleaseArtifact(platform, architecture, Version, Channel);
        var packagesDir = GetPackagesDir();

        if (platform != AbpStudioPlatform.Linux && !Directory.Exists(installPath))
        {
            Directory.CreateDirectory(installPath);
        }

        AnsiConsole.MarkupLine("----------------------------------------");
        AnsiConsole.MarkupLine($"[blue]Switching to ABP Studio version {Version} on channel {Channel}...[/]");
        AnsiConsole.MarkupLine("----------------------------------------");
        AnsiConsole.MarkupLine($"[dim]Installing to {Markup.Escape(installPath)}[/]");

        if (platform == AbpStudioPlatform.Linux && PackagesDir == null)
        {
            LinuxAbpStudioInstaller.EnsurePrivatePackagesDirectory(packagesDir);
        }
        else
        {
            Directory.CreateDirectory(packagesDir);
        }

        var outputPath = Path.Combine(packagesDir, release.FileName);
        await TryDownloadAsync(release.FileName, outputPath, release.DownloadUrl);

        AnsiConsole.MarkupLine("----------------------------------------");

        if (platform == AbpStudioPlatform.Linux)
        {
            await LinuxAbpStudioInstaller.InstallAsync(
                outputPath,
                installPath,
                Version,
                Channel,
                release.RuntimeIdentifier);

            await ReportSuccessAsync(restartRequired: true);
            return;
        }

        var updateExePath = GetUpdateExecutablePath(installPath);

        if (!File.Exists(updateExePath))
        {
            AnsiConsole.MarkupLine($"[red]Error: Platform updater not found at {Markup.Escape(updateExePath)}[/]");
            AnsiConsole.MarkupLine("[red]ABP Studio may not be installed or the installation is corrupted.[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[dim]Running {Markup.Escape(updateExePath)} apply --package {Markup.Escape(outputPath)}[/]");

        var startInfo = CreateUpdaterProcessStartInfo(updateExePath, outputPath, installPath);

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
            await ReportSuccessAsync(restartRequired: false);
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
        // Check if file already exists
        if (!Force && File.Exists(outputPath))
        {
            AnsiConsole.MarkupLine($"[yellow]File {Markup.Escape(fileName)} already exists. Skipping download.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[dim]Starting download from {Markup.Escape(url)}[/]");

            var partialPath = $"{outputPath}.{Guid.NewGuid():N}.partial";

            try
            {
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

                        await using var contentStream = await response.Content.ReadAsStreamAsync();
                        await using var fileStream = new FileStream(
                            partialPath,
                            FileMode.CreateNew,
                            FileAccess.Write,
                            FileShare.None,
                            bufferSize: 8192,
                            FileOptions.SequentialScan);

                        int bytesRead;
                        while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                        {
                            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
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

                        await fileStream.FlushAsync();
                        fileStream.Flush(flushToDisk: true);

                        if (totalBytes >= 0 && downloadedBytes != totalBytes)
                        {
                            throw new InvalidDataException(
                                $"Downloaded {downloadedBytes} bytes, but the server declared {totalBytes} bytes.");
                        }
                    });

                File.Move(partialPath, outputPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(partialPath))
                {
                    File.Delete(partialPath);
                }
            }

            AnsiConsole.MarkupLine($"[green]Downloaded {Markup.Escape(fileName)} successfully.[/]");
        }
    }

    private async Task ReportSuccessAsync(bool restartRequired)
    {
        AnsiConsole.MarkupLine("----------------------------------------");
        AnsiConsole.MarkupLine(
            $"[green]ABP Studio version {Markup.Escape(Version)} on channel {Markup.Escape(Channel)} installed successfully.[/]");
        if (restartRequired)
        {
            AnsiConsole.MarkupLine("[yellow]Close and reopen ABP Studio to run the selected version.[/]");
        }
        AnsiConsole.MarkupLine("----------------------------------------");

        await notificationManager.SendAsync(
            "ABP Studio Updated",
            $"Successfully switched to ABP Studio version {Version} on channel {Channel}");
    }

    protected virtual string GetInstallDir()
    {
        if (InstallDir != null)
        {
            return InstallDir;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var globalPath = "/Applications/ABP Studio.app";

            if (Directory.Exists(globalPath))
            {
                return globalPath;
            }

            var userProfilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications", "ABP Studio.app");
            if (Directory.Exists(userProfilePath))
            {
                return userProfilePath;
            }
            throw new DirectoryNotFoundException($"ABP Studio installation not found in {globalPath} or {userProfilePath}.");
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
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "abp-studio", "packages");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return LinuxAbpStudioInstaller.GetDefaultPackagesDirectory();
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "abp-studio", "packages");
    }

    protected virtual string GetUpdateExecutablePath(string installDir)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Path.Combine(installDir, "Contents", "MacOS", "UpdateMac");
        }

        return Path.Combine(installDir, "Update.exe");
    }

    internal static ProcessStartInfo CreateUpdaterProcessStartInfo(
        string updaterPath,
        string packagePath,
        string installDir)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = updaterPath,
            WorkingDirectory = installDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("apply");
        startInfo.ArgumentList.Add("--package");
        startInfo.ArgumentList.Add(packagePath);

        return startInfo;
    }

    internal static AbpStudioReleaseArtifact ResolveReleaseArtifact(
        AbpStudioPlatform platform,
        Architecture architecture,
        string version,
        string channel)
    {
        ValidateArtifactComponent(version, nameof(version));
        ValidateArtifactComponent(channel, nameof(channel));

        var isArm = architecture is Architecture.Arm or Architecture.Arm64
#if NET7_0_OR_GREATER
            or Architecture.Armv6
#endif
            ;

        var platformAlias = platform switch
        {
            AbpStudioPlatform.Windows => isArm ? "windows-arm" : "windows",
            AbpStudioPlatform.MacOS => isArm ? "osx" : "osx-intel",
            AbpStudioPlatform.Linux when architecture == Architecture.X64 => "linux",
            AbpStudioPlatform.Linux when architecture == Architecture.Arm64 => "linux-arm64",
            AbpStudioPlatform.Linux => throw new PlatformNotSupportedException(
                $"Linux architecture '{architecture}' is not supported by ABP Studio packages."),
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null)
        };
        var packageId = platform == AbpStudioPlatform.Linux ? "AbpStudio" : "abp-studio";
        var runtimeIdentifier = platform switch
        {
            AbpStudioPlatform.Linux when architecture == Architecture.X64 => "linux-x64",
            AbpStudioPlatform.Linux when architecture == Architecture.Arm64 => "linux-arm64",
            _ => string.Empty
        };
        var fileName = $"{packageId}-{version}-{channel}-full.nupkg";
        var downloadUrl = $"https://abp.io/api/abp-studio/r/download/{platformAlias}/{Uri.EscapeDataString(fileName)}";

        return new AbpStudioReleaseArtifact(
            platformAlias,
            packageId,
            fileName,
            downloadUrl,
            runtimeIdentifier);
    }

    private static void ValidateArtifactComponent(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Any(character => !char.IsLetterOrDigit(character) && character is not ('.' or '-' or '_' or '+')))
        {
            throw new ArgumentException(
                "Only letters, digits, dots, hyphens, underscores, and plus signs are allowed.",
                parameterName);
        }
    }

    private static AbpStudioPlatform GetCurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return AbpStudioPlatform.Windows;
        }

        if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst())
        {
            return AbpStudioPlatform.MacOS;
        }

        if (OperatingSystem.IsLinux())
        {
            return AbpStudioPlatform.Linux;
        }

        throw new PlatformNotSupportedException("ABP Studio switching is supported only on Windows, macOS, and Linux.");
    }

    public static string OperatingSystemAlias
    {
        get
        {
            return ResolveReleaseArtifact(
                    GetCurrentPlatform(),
                    RuntimeInformation.OSArchitecture,
                    "1.0.0",
                    "stable")
                .PlatformAlias;
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

internal enum AbpStudioPlatform
{
    Windows,
    MacOS,
    Linux
}

internal sealed record AbpStudioReleaseArtifact(
    string PlatformAlias,
    string PackageId,
    string FileName,
    string DownloadUrl,
    string RuntimeIdentifier);
