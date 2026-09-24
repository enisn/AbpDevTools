using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using AbpDevTools.Commands;
using FluentAssertions;
using Xunit;

namespace AbpDevTools.Tests.Commands;

public sealed class AbpStudioSwitchCommandTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(
        Path.GetTempPath(),
        $"abpdev-studio-switch-{Guid.NewGuid():N}");

    public AbpStudioSwitchCommandTests()
    {
        Directory.CreateDirectory(tempDirectory);
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(
                tempDirectory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Theory]
    [InlineData(AbpStudioPlatform.Windows, Architecture.X64, "windows", "abp-studio", "")]
    [InlineData(AbpStudioPlatform.Windows, Architecture.Arm64, "windows-arm", "abp-studio", "")]
    [InlineData(AbpStudioPlatform.MacOS, Architecture.X64, "osx-intel", "abp-studio", "")]
    [InlineData(AbpStudioPlatform.MacOS, Architecture.Arm64, "osx", "abp-studio", "")]
    [InlineData(AbpStudioPlatform.Linux, Architecture.X64, "linux", "AbpStudio", "linux-x64")]
    [InlineData(AbpStudioPlatform.Linux, Architecture.Arm64, "linux-arm64", "AbpStudio", "linux-arm64")]
    internal void ResolveReleaseArtifact_Should_Use_Platform_Publishing_Convention(
        AbpStudioPlatform platform,
        Architecture architecture,
        string expectedPlatformAlias,
        string expectedPackageId,
        string expectedRuntimeIdentifier)
    {
        var artifact = AbpStudioSwitchCommand.ResolveReleaseArtifact(
            platform,
            architecture,
            "3.0.10",
            "stable");

        artifact.PlatformAlias.Should().Be(expectedPlatformAlias);
        artifact.PackageId.Should().Be(expectedPackageId);
        artifact.FileName.Should().Be($"{expectedPackageId}-3.0.10-stable-full.nupkg");
        artifact.DownloadUrl.Should().Be(
            $"https://abp.io/api/abp-studio/r/download/{expectedPlatformAlias}/{expectedPackageId}-3.0.10-stable-full.nupkg");
        artifact.RuntimeIdentifier.Should().Be(expectedRuntimeIdentifier);
    }

    [Fact]
    public void ResolveReleaseArtifact_Should_Reject_Unsupported_Linux_Architecture()
    {
        var action = () => AbpStudioSwitchCommand.ResolveReleaseArtifact(
            AbpStudioPlatform.Linux,
            Architecture.X86,
            "3.0.10",
            "stable");

        action.Should().Throw<PlatformNotSupportedException>();
    }

    [Fact]
    public void ResolveReleaseArtifact_Should_Reject_Unsafe_File_Name_Components()
    {
        var action = () => AbpStudioSwitchCommand.ResolveReleaseArtifact(
            AbpStudioPlatform.Linux,
            Architecture.X64,
            "../3.0.10",
            "stable");

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetDefaultPackagesDirectory_Should_Use_Per_User_Cache()
    {
        var homeDirectory = Path.Combine(tempDirectory, "home");

        LinuxAbpStudioInstaller.GetDefaultPackagesDirectory(homeDirectory)
            .Should().Be(Path.Combine(homeDirectory, ".abpdev", "cache", "AbpStudio", "packages"));
    }

    [Fact]
    public void EnsurePrivatePackagesDirectory_Should_Remove_Group_And_Other_Access()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var abpdevDirectory = Path.Combine(tempDirectory, ".abpdev");
        var cacheDirectory = Path.Combine(abpdevDirectory, "cache");
        var studioDirectory = Path.Combine(cacheDirectory, "AbpStudio");
        var packagesDirectory = Path.Combine(studioDirectory, "packages");
        Directory.CreateDirectory(packagesDirectory);
        var unsafeMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
        File.SetUnixFileMode(abpdevDirectory, unsafeMode);
        File.SetUnixFileMode(cacheDirectory, unsafeMode);
        File.SetUnixFileMode(studioDirectory, unsafeMode);
        File.SetUnixFileMode(packagesDirectory, unsafeMode);

        LinuxAbpStudioInstaller.EnsurePrivatePackagesDirectory(packagesDirectory, tempDirectory);

        var expectedMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        File.GetUnixFileMode(abpdevDirectory).Should().Be(expectedMode);
        File.GetUnixFileMode(cacheDirectory).Should().Be(expectedMode);
        File.GetUnixFileMode(studioDirectory).Should().Be(expectedMode);
        File.GetUnixFileMode(packagesDirectory).Should().Be(expectedMode);
    }

    [Fact]
    public void EnsurePrivatePackagesDirectory_Should_Reject_Writable_Home_Directory()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var homeDirectory = Path.Combine(tempDirectory, "unsafe-home");
        var packagesDirectory = Path.Combine(homeDirectory, ".abpdev", "cache", "AbpStudio", "packages");
        Directory.CreateDirectory(homeDirectory);
        File.SetUnixFileMode(
            homeDirectory,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite);

        var action = () => LinuxAbpStudioInstaller.EnsurePrivatePackagesDirectory(
            packagesDirectory,
            homeDirectory);

        action.Should().Throw<UnauthorizedAccessException>()
            .WithMessage("*writable by another user*");
    }

    [Fact]
    public void ResolveInstalledAppImage_Should_Prefer_Explicit_Path()
    {
        var explicitPath = CreateAppImage("explicit/Custom.AppImage", Architecture.X64, 1);
        var environmentPath = CreateAppImage("environment/AbpStudio.AppImage", Architecture.X64, 2);

        var result = LinuxAbpStudioInstaller.ResolveInstalledAppImage(
            explicitPath,
            environmentPath,
            Array.Empty<string>(),
            Array.Empty<string>());

        result.Should().Be(Path.GetFullPath(explicitPath));
    }

    [Fact]
    public void ResolveInstalledAppImage_Should_Accept_Containing_Directory()
    {
        var expectedPath = CreateAppImage("installation/AbpStudio.AppImage", Architecture.X64, 1);

        var result = LinuxAbpStudioInstaller.ResolveInstalledAppImage(
            Path.GetDirectoryName(expectedPath),
            null,
            Array.Empty<string>(),
            Array.Empty<string>());

        result.Should().Be(Path.GetFullPath(expectedPath));
    }

    [Fact]
    public void ResolveInstalledAppImage_Should_Read_Quoted_Desktop_Entry()
    {
        var expectedPath = CreateAppImage("ABP Studio/AbpStudio.AppImage", Architecture.X64, 1);
        var desktopEntryPath = Path.Combine(tempDirectory, "abp-studio.desktop");
        File.WriteAllText(
            desktopEntryPath,
            $"[Desktop Entry]\nName=ABP Studio\nExec=\"{expectedPath}\" %U\n");

        var result = LinuxAbpStudioInstaller.ResolveInstalledAppImage(
            null,
            null,
            new[] { desktopEntryPath },
            Array.Empty<string>());

        result.Should().Be(Path.GetFullPath(expectedPath));
    }

    [Fact]
    public void ResolveInstalledAppImage_Should_Not_Fall_Back_From_Invalid_Explicit_Path()
    {
        var fallbackPath = CreateAppImage("fallback/AbpStudio.AppImage", Architecture.X64, 1);
        var missingPath = Path.Combine(tempDirectory, "missing.AppImage");

        var action = () => LinuxAbpStudioInstaller.ResolveInstalledAppImage(
            missingPath,
            fallbackPath,
            Array.Empty<string>(),
            Array.Empty<string>());

        action.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void TryParseDesktopEntryExecutable_Should_Handle_Escaped_Spaces_And_Field_Codes()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var result = LinuxAbpStudioInstaller.TryParseDesktopEntryExecutable(
            "[Desktop Entry]\nExec=/opt/ABP\\ Studio/AbpStudio.AppImage %U\n");

        result.Should().Be("/opt/ABP Studio/AbpStudio.AppImage");
    }

    [Fact]
    public async Task InstallAsync_Should_Validate_And_Atomically_Replace_AppImage()
    {
        var originalBytes = CreateAppImageBytes(Architecture.X64, 1);
        var replacementBytes = CreateAppImageBytes(Architecture.X64, 2);
        var targetPath = Path.Combine(tempDirectory, "install", "AbpStudio.AppImage");
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        await File.WriteAllBytesAsync(targetPath, originalBytes);
        UnixFileMode? originalMode = null;
        if (!OperatingSystem.IsWindows())
        {
            originalMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute;
            File.SetUnixFileMode(targetPath, originalMode.Value);
        }
        var packagePath = CreatePackage("AbpStudio", "3.0.10", "stable", "linux-x64", "x64", replacementBytes);

        await LinuxAbpStudioInstaller.InstallAsync(
            packagePath,
            targetPath,
            "3.0.10",
            "stable",
            "linux-x64",
            appImageRuntimeVerifier: (_, payloadOffset, _) =>
            {
                payloadOffset.Should().Be(4160);
                return Task.CompletedTask;
            });

        (await File.ReadAllBytesAsync(targetPath)).Should().Equal(replacementBytes);
        Directory.GetFiles(Path.GetDirectoryName(targetPath)!, "*.abpdev-stage").Should().BeEmpty();

        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(targetPath).Should().Be(originalMode);
        }
    }

    [Fact]
    public async Task InstallAsync_Should_Keep_Current_AppImage_When_Metadata_Is_Invalid()
    {
        var originalBytes = CreateAppImageBytes(Architecture.X64, 1);
        var targetPath = Path.Combine(tempDirectory, "install", "AbpStudio.AppImage");
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        await File.WriteAllBytesAsync(targetPath, originalBytes);
        var packagePath = CreatePackage("abp-studio", "3.0.10", "stable", "linux-x64", "x64", CreateAppImageBytes(Architecture.X64, 2));

        var action = () => LinuxAbpStudioInstaller.InstallAsync(
            packagePath,
            targetPath,
            "3.0.10",
            "stable",
            "linux-x64");

        await action.Should().ThrowAsync<InvalidDataException>();
        (await File.ReadAllBytesAsync(targetPath)).Should().Equal(originalBytes);
        Directory.GetFiles(Path.GetDirectoryName(targetPath)!, "*.abpdev-stage").Should().BeEmpty();
    }

    [Fact]
    public async Task InstallAsync_Should_Keep_Current_AppImage_When_Payload_Is_Truncated()
    {
        var originalBytes = CreateAppImageBytes(Architecture.X64, 1);
        var targetPath = Path.Combine(tempDirectory, "install", "AbpStudio.AppImage");
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        await File.WriteAllBytesAsync(targetPath, originalBytes);
        var truncatedBytes = CreateAppImageBytes(Architecture.X64, 2)[..64];
        var packagePath = CreatePackage("AbpStudio", "3.0.10", "stable", "linux-x64", "x64", truncatedBytes);

        var action = () => LinuxAbpStudioInstaller.InstallAsync(
            packagePath,
            targetPath,
            "3.0.10",
            "stable",
            "linux-x64");

        await action.Should().ThrowAsync<InvalidDataException>();
        (await File.ReadAllBytesAsync(targetPath)).Should().Equal(originalBytes);
        Directory.GetFiles(Path.GetDirectoryName(targetPath)!, "*.abpdev-stage").Should().BeEmpty();
    }

    [Fact]
    public async Task InstallAsync_Should_Keep_Current_AppImage_When_Runtime_Check_Fails()
    {
        var originalBytes = CreateAppImageBytes(Architecture.X64, 1);
        var targetPath = Path.Combine(tempDirectory, "install", "AbpStudio.AppImage");
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        await File.WriteAllBytesAsync(targetPath, originalBytes);
        var packagePath = CreatePackage(
            "AbpStudio",
            "3.0.10",
            "stable",
            "linux-x64",
            "x64",
            CreateAppImageBytes(Architecture.X64, 2));

        var action = () => LinuxAbpStudioInstaller.InstallAsync(
            packagePath,
            targetPath,
            "3.0.10",
            "stable",
            "linux-x64",
            appImageRuntimeVerifier: (_, _, _) => Task.FromException(
                new InvalidDataException("AppImage runtime check failed.")));

        await action.Should().ThrowAsync<InvalidDataException>();
        (await File.ReadAllBytesAsync(targetPath)).Should().Equal(originalBytes);
        Directory.GetFiles(Path.GetDirectoryName(targetPath)!, "*.abpdev-stage").Should().BeEmpty();
    }

    [Fact]
    public async Task InstallAsync_Should_Reject_Concurrent_Switches()
    {
        var targetPath = CreateAppImage("install/AbpStudio.AppImage", Architecture.X64, 1);
        var packagePath = CreatePackage(
            "AbpStudio",
            "3.0.10",
            "stable",
            "linux-x64",
            "x64",
            CreateAppImageBytes(Architecture.X64, 2));
        var lockPath = Path.Combine(Path.GetDirectoryName(targetPath)!, ".abpdev-abp-studio-switch.lock");
        using var heldLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        var action = () => LinuxAbpStudioInstaller.InstallAsync(
            packagePath,
            targetPath,
            "3.0.10",
            "stable",
            "linux-x64");

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Another ABP Studio switch is already in progress*");
    }

    [Fact]
    public async Task VerifyAppImageRuntimeAsync_Should_Time_Out_When_Descendant_Holds_Output_Pipes()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var probePath = CreateExecutableScript(
            "descendant-probe.sh",
            "sleep 2 &\nprintf '4160\\n'\n");
        var stopwatch = Stopwatch.StartNew();

        var action = () => LinuxAbpStudioInstaller.VerifyAppImageRuntimeAsync(
            probePath,
            4160,
            CancellationToken.None,
            TimeSpan.FromMilliseconds(250));

        await action.Should().ThrowAsync<TimeoutException>();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task VerifyAppImageRuntimeAsync_Should_Reject_Excessive_Output()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var probePath = CreateExecutableScript(
            "large-output-probe.sh",
            $"printf '{new string('1', 256)}\\n'\n");

        var action = () => LinuxAbpStudioInstaller.VerifyAppImageRuntimeAsync(
            probePath,
            4160,
            CancellationToken.None,
            TimeSpan.FromSeconds(2));

        await action.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*produced too much output*");
    }

    [Fact]
    public void CreateUpdaterProcessStartInfo_Should_Preserve_Paths_With_Spaces()
    {
        var updaterPath = Path.Combine(tempDirectory, "ABP Studio", "Update.exe");
        var packagePath = Path.Combine(tempDirectory, "package cache", "abp-studio.nupkg");
        var installDirectory = Path.Combine(tempDirectory, "ABP Studio");

        var startInfo = AbpStudioSwitchCommand.CreateUpdaterProcessStartInfo(
            updaterPath,
            packagePath,
            installDirectory);

        startInfo.FileName.Should().Be(updaterPath);
        startInfo.WorkingDirectory.Should().Be(installDirectory);
        startInfo.ArgumentList.Should().Equal("apply", "--package", packagePath);
        startInfo.UseShellExecute.Should().BeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private string CreateAppImage(string relativePath, Architecture architecture, byte marker)
    {
        var path = Path.Combine(tempDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, CreateAppImageBytes(architecture, marker));
        return path;
    }

    private string CreateExecutableScript(string fileName, string body)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException();
        }

        var path = Path.Combine(tempDirectory, fileName);
        File.WriteAllText(path, $"#!/bin/sh\n{body}");
        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }

    private string CreatePackage(
        string packageId,
        string version,
        string channel,
        string runtimeIdentifier,
        string machineArchitecture,
        byte[] appImageBytes)
    {
        var packagePath = Path.Combine(tempDirectory, $"{Guid.NewGuid():N}.nupkg");
        using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);

        var nuspecEntry = archive.CreateEntry("AbpStudio.nuspec");
        using (var writer = new StreamWriter(nuspecEntry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            writer.Write(
                $"""
                <?xml version="1.0" encoding="utf-8"?>
                <package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
                  <metadata>
                    <id>{packageId}</id>
                    <version>{version}</version>
                    <channel>{channel}</channel>
                    <os>linux</os>
                    <rid>{runtimeIdentifier}</rid>
                    <machineArchitecture>{machineArchitecture}</machineArchitecture>
                  </metadata>
                </package>
                """);
        }

        var appImageEntry = archive.CreateEntry("lib/app/AbpStudio.AppImage", CompressionLevel.NoCompression);
        using (var stream = appImageEntry.Open())
        {
            stream.Write(appImageBytes);
        }

        return packagePath;
    }

    private static byte[] CreateAppImageBytes(Architecture architecture, byte marker)
    {
        var bytes = new byte[1024 * 1024];
        bytes[0] = 0x7f;
        bytes[1] = (byte)'E';
        bytes[2] = (byte)'L';
        bytes[3] = (byte)'F';
        bytes[4] = 2;
        bytes[5] = 1;
        bytes[6] = 1;
        bytes[8] = (byte)'A';
        bytes[9] = (byte)'I';
        bytes[10] = 2;

        bytes[16] = 3;
        bytes[20] = 1;
        bytes[24] = 1;
        bytes[32] = 64;
        bytes[41] = 16;
        bytes[52] = 64;
        bytes[54] = 56;
        bytes[56] = 1;
        bytes[58] = 64;
        bytes[60] = 1;

        var machine = architecture switch
        {
            Architecture.X64 => 62,
            Architecture.Arm64 => 183,
            _ => throw new ArgumentOutOfRangeException(nameof(architecture), architecture, null)
        };
        bytes[18] = (byte)(machine & 0xff);
        bytes[19] = (byte)(machine >> 8);
        bytes[64] = 1;
        bytes[68] = 5;
        bytes[97] = 32;
        bytes[105] = 32;
        bytes[113] = 16;
        bytes[1024] = marker;

        const int payloadOffset = 4160;
        bytes[payloadOffset] = (byte)'h';
        bytes[payloadOffset + 1] = (byte)'s';
        bytes[payloadOffset + 2] = (byte)'q';
        bytes[payloadOffset + 3] = (byte)'s';
        bytes[payloadOffset + 4] = 1;
        bytes[payloadOffset + 13] = 16;
        bytes[payloadOffset + 20] = 1;
        bytes[payloadOffset + 22] = 12;
        bytes[payloadOffset + 26] = 1;
        bytes[payloadOffset + 28] = 4;

        var payloadLength = (ulong)(bytes.Length - payloadOffset);
        for (var index = 0; index < sizeof(ulong); index++)
        {
            bytes[payloadOffset + 40 + index] = (byte)(payloadLength >> (8 * index));
        }

        return bytes;
    }
}
