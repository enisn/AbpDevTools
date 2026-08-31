using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace AbpDevTools.Commands;

internal static class LinuxAbpStudioInstaller
{
    private const string AppImageEntryName = "lib/app/AbpStudio.AppImage";
    private const string NuspecEntryName = "AbpStudio.nuspec";
    private const string InstallLockFileName = ".abpdev-abp-studio-switch.lock";
    private const long MinimumAppImageLength = 1024 * 1024;
    private const int SquashFsSuperblockLength = 96;
    private const int MaximumRuntimeCheckOutputLength = 128;
    private const UnixFileMode PrivateDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    internal static string GetDefaultPackagesDirectory()
    {
        return GetDefaultPackagesDirectory(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    internal static string GetDefaultPackagesDirectory(string homeDirectory)
    {
        var fullHomeDirectory = Path.GetFullPath(homeDirectory);
        return Path.Combine(fullHomeDirectory, ".abpdev", "cache", "AbpStudio", "packages");
    }

    internal static void EnsurePrivatePackagesDirectory(string packagesDirectory)
    {
        EnsurePrivatePackagesDirectory(
            packagesDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    internal static void EnsurePrivatePackagesDirectory(string packagesDirectory, string homeDirectory)
    {
        if (!OperatingSystem.IsLinux())
        {
            Directory.CreateDirectory(packagesDirectory);
            return;
        }

        var fullHomeDirectory = Path.GetFullPath(homeDirectory);
        var fullPackagesDirectory = Path.GetFullPath(packagesDirectory);
        if (!IsPathWithin(fullHomeDirectory, fullPackagesDirectory))
        {
            throw new InvalidOperationException(
                $"The default ABP Studio package cache '{fullPackagesDirectory}' must be inside the current user's home directory.");
        }

        var packages = new DirectoryInfo(fullPackagesDirectory);
        var studio = packages.Parent;
        var cache = studio?.Parent;
        var abpdev = cache?.Parent;
        if (!string.Equals(packages.Name, "packages", StringComparison.Ordinal)
            || !string.Equals(studio?.Name, "AbpStudio", StringComparison.Ordinal)
            || !string.Equals(cache?.Name, "cache", StringComparison.Ordinal)
            || !string.Equals(abpdev?.Name, ".abpdev", StringComparison.Ordinal)
            || !string.Equals(abpdev?.Parent?.FullName, fullHomeDirectory, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The default ABP Studio package cache '{fullPackagesDirectory}' has an unexpected layout.");
        }

        ValidateSecureAncestry(fullHomeDirectory);
        EnsurePrivateDirectory(abpdev!.FullName);
        EnsurePrivateDirectory(cache!.FullName);
        EnsurePrivateDirectory(studio!.FullName);
        EnsurePrivateDirectory(packages.FullName);
    }

    private static bool IsPathWithin(string parentPath, string candidatePath)
    {
        var relativePath = Path.GetRelativePath(parentPath, candidatePath);
        return !Path.IsPathRooted(relativePath)
            && !string.Equals(relativePath, "..", StringComparison.Ordinal)
            && !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    [SupportedOSPlatform("linux")]
    private static void ValidateSecureAncestry(string directoryPath)
    {
        var ancestry = new Stack<DirectoryInfo>();
        for (var directory = new DirectoryInfo(directoryPath); directory != null; directory = directory.Parent)
        {
            ancestry.Push(directory);
        }

        while (ancestry.TryPop(out var directory))
        {
            ValidateSecureDirectory(directory, allowStickyWritableDirectory: true);
        }
    }

    [SupportedOSPlatform("linux")]
    private static void EnsurePrivateDirectory(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            var directory = new DirectoryInfo(directoryPath);
            if (directory.LinkTarget != null)
            {
                throw new InvalidOperationException(
                    $"The default ABP Studio package cache directory '{directoryPath}' must not be a symbolic link.");
            }
        }
        else
        {
            Directory.CreateDirectory(directoryPath, PrivateDirectoryMode);
        }

        File.SetUnixFileMode(directoryPath, PrivateDirectoryMode);
    }

    [SupportedOSPlatform("linux")]
    private static void ValidateSecureDirectory(DirectoryInfo directory, bool allowStickyWritableDirectory)
    {
        if (!directory.Exists)
        {
            throw new DirectoryNotFoundException(
                $"The package cache ancestor '{directory.FullName}' does not exist.");
        }

        if (directory.LinkTarget != null)
        {
            throw new InvalidOperationException(
                $"The package cache ancestor '{directory.FullName}' must not be a symbolic link.");
        }

        var mode = File.GetUnixFileMode(directory.FullName);
        var writableByOthers = (mode & (UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) != 0;
        var hasStickyBit = (mode & UnixFileMode.StickyBit) != 0;
        if (writableByOthers && (!allowStickyWritableDirectory || !hasStickyBit))
        {
            throw new UnauthorizedAccessException(
                $"The package cache ancestor '{directory.FullName}' is writable by another user.");
        }
    }

    internal static string ResolveInstalledAppImage(string? configuredPath)
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrWhiteSpace(xdgDataHome))
        {
            xdgDataHome = Path.Combine(homeDirectory, ".local", "share");
        }

        var desktopEntryPaths = new List<string>
        {
            Path.Combine(xdgDataHome, "applications", "abp-studio.desktop")
        };

        var xdgDataDirectories = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
        if (string.IsNullOrWhiteSpace(xdgDataDirectories))
        {
            xdgDataDirectories = "/usr/local/share:/usr/share";
        }

        desktopEntryPaths.AddRange(
            xdgDataDirectories
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(directory => Path.Combine(directory, "applications", "abp-studio.desktop")));

        var knownPaths = new[]
        {
            Path.Combine(homeDirectory, ".local", "opt", "abp-studio", "AbpStudio.AppImage"),
            Path.Combine(homeDirectory, "Applications", "AbpStudio.AppImage"),
            "/opt/abp-studio/AbpStudio.AppImage"
        };

        return ResolveInstalledAppImage(
            configuredPath,
            Environment.GetEnvironmentVariable("APPIMAGE"),
            desktopEntryPaths,
            knownPaths);
    }

    internal static string ResolveInstalledAppImage(
        string? configuredPath,
        string? appImageEnvironmentPath,
        IEnumerable<string> desktopEntryPaths,
        IEnumerable<string> knownPaths)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return ResolveExplicitPath(configuredPath);
        }

        if (TryResolveCandidate(appImageEnvironmentPath, requireAbpStudioFileName: true, out var environmentPath))
        {
            return environmentPath;
        }

        foreach (var desktopEntryPath in desktopEntryPaths)
        {
            if (!File.Exists(desktopEntryPath))
            {
                continue;
            }

            string? executablePath;
            try
            {
                executablePath = TryParseDesktopEntryExecutable(File.ReadAllText(desktopEntryPath));
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            if (TryResolveCandidate(executablePath, requireAbpStudioFileName: true, out var desktopPath))
            {
                return desktopPath;
            }
        }

        foreach (var knownPath in knownPaths)
        {
            if (TryResolveCandidate(knownPath, requireAbpStudioFileName: true, out var resolvedKnownPath))
            {
                return resolvedKnownPath;
            }
        }

        throw new FileNotFoundException(
            "Could not find the installed ABP Studio AppImage. Pass its file path or containing directory with --install-dir.");
    }

    internal static string? TryParseDesktopEntryExecutable(string desktopEntry)
    {
        using var reader = new StringReader(desktopEntry);
        var inDesktopEntrySection = false;

        while (reader.ReadLine() is { } line)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.Length == 0 || trimmedLine.StartsWith('#'))
            {
                continue;
            }

            if (trimmedLine.StartsWith('[') && trimmedLine.EndsWith(']'))
            {
                inDesktopEntrySection = string.Equals(trimmedLine, "[Desktop Entry]", StringComparison.Ordinal);
                continue;
            }

            if (!inDesktopEntrySection || !trimmedLine.StartsWith("Exec=", StringComparison.Ordinal))
            {
                continue;
            }

            var executable = ParseFirstDesktopExecToken(trimmedLine[5..]);
            if (string.IsNullOrWhiteSpace(executable)
                || !Path.IsPathRooted(executable)
                || executable.Contains('%'))
            {
                return null;
            }

            return executable;
        }

        return null;
    }

    internal static Architecture ReadAppImageArchitecture(string appImagePath)
    {
        return ReadAppImageLayout(appImagePath).Architecture;
    }

    internal static Architecture ValidateAppImage(string appImagePath)
    {
        return ValidateAppImageLayout(appImagePath).Architecture;
    }

    private static AppImageLayout ReadAppImageLayout(string appImagePath)
    {
        Span<byte> header = stackalloc byte[64];
        using var stream = new FileStream(appImagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Read(header) != header.Length
            || header[0] != 0x7f
            || header[1] != (byte)'E'
            || header[2] != (byte)'L'
            || header[3] != (byte)'F'
            || header[4] != 2
            || header[5] is not (1 or 2)
            || header[6] != 1
            || header[8] != (byte)'A'
            || header[9] != (byte)'I'
            || header[10] != 2)
        {
            throw new InvalidDataException($"'{appImagePath}' is not a supported 64-bit AppImage.");
        }

        var littleEndian = header[5] == 1;
        var elfType = ReadUInt16(header, 16, littleEndian);
        var machine = ReadUInt16(header, 18, littleEndian);
        var elfVersion = ReadUInt32(header, 20, littleEndian);
        var entryPoint = ReadUInt64(header, 24, littleEndian);
        var programHeaderOffset = ReadUInt64(header, 32, littleEndian);
        var sectionHeaderOffset = ReadUInt64(header, 40, littleEndian);
        var elfHeaderSize = ReadUInt16(header, 52, littleEndian);
        var programHeaderEntrySize = ReadUInt16(header, 54, littleEndian);
        var programHeaderCount = ReadUInt16(header, 56, littleEndian);
        var sectionHeaderEntrySize = ReadUInt16(header, 58, littleEndian);
        var sectionHeaderCount = ReadUInt16(header, 60, littleEndian);

        if (elfType is not (2 or 3)
            || elfVersion != 1
            || entryPoint == 0
            || elfHeaderSize < header.Length
            || programHeaderOffset < (ulong)elfHeaderSize
            || programHeaderEntrySize < 56
            || programHeaderCount == 0
            || programHeaderOffset > (ulong)stream.Length
            || (ulong)programHeaderEntrySize * programHeaderCount > (ulong)stream.Length - programHeaderOffset
            || sectionHeaderOffset < (ulong)elfHeaderSize
            || sectionHeaderEntrySize < 64
            || sectionHeaderCount == 0
            || sectionHeaderOffset > (ulong)stream.Length
            || (ulong)sectionHeaderEntrySize * sectionHeaderCount > (ulong)stream.Length - sectionHeaderOffset)
        {
            throw new InvalidDataException($"'{appImagePath}' has an invalid ELF executable header.");
        }

        var executableSegmentContainsEntryPoint = false;
        var programHeader = new byte[programHeaderEntrySize];
        for (var index = 0; index < programHeaderCount; index++)
        {
            stream.Position = checked((long)(programHeaderOffset + (ulong)index * programHeaderEntrySize));
            stream.ReadExactly(programHeader);

            var type = ReadUInt32(programHeader, 0, littleEndian);
            if (type != 1)
            {
                continue;
            }

            var flags = ReadUInt32(programHeader, 4, littleEndian);
            var fileOffset = ReadUInt64(programHeader, 8, littleEndian);
            var virtualAddress = ReadUInt64(programHeader, 16, littleEndian);
            var fileSize = ReadUInt64(programHeader, 32, littleEndian);
            var memorySize = ReadUInt64(programHeader, 40, littleEndian);

            if (memorySize < fileSize
                || fileOffset > (ulong)stream.Length
                || fileSize > (ulong)stream.Length - fileOffset
                || memorySize > ulong.MaxValue - virtualAddress)
            {
                throw new InvalidDataException($"'{appImagePath}' has an invalid ELF load segment.");
            }

            if ((flags & 1) != 0
                && entryPoint >= virtualAddress
                && entryPoint < virtualAddress + memorySize)
            {
                executableSegmentContainsEntryPoint = true;
            }
        }

        if (!executableSegmentContainsEntryPoint)
        {
            throw new InvalidDataException(
                $"'{appImagePath}' has no executable ELF load segment containing its entry point.");
        }

        var payloadOffset = sectionHeaderOffset + (ulong)sectionHeaderEntrySize * sectionHeaderCount;
        if (stream.Length < SquashFsSuperblockLength
            || payloadOffset > long.MaxValue
            || payloadOffset > (ulong)(stream.Length - SquashFsSuperblockLength))
        {
            throw new InvalidDataException($"'{appImagePath}' has an invalid AppImage payload offset.");
        }

        var architecture = machine switch
        {
            62 => Architecture.X64,
            183 => Architecture.Arm64,
            _ => throw new PlatformNotSupportedException(
                $"The ABP Studio AppImage at '{appImagePath}' uses unsupported ELF machine type {machine}.")
        };

        return new AppImageLayout(architecture, checked((long)payloadOffset));
    }

    private static AppImageLayout ValidateAppImageLayout(string appImagePath)
    {
        var layout = ReadAppImageLayout(appImagePath);
        using var stream = new FileStream(appImagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length < MinimumAppImageLength)
        {
            throw new InvalidDataException(
                $"'{appImagePath}' does not contain a complete AppImage filesystem payload.");
        }

        Span<byte> superblock = stackalloc byte[SquashFsSuperblockLength];
        stream.Position = layout.PayloadOffset;
        stream.ReadExactly(superblock);

        var inodeCount = ReadUInt32(superblock, 4, littleEndian: true);
        var blockSize = ReadUInt32(superblock, 12, littleEndian: true);
        var compressionId = ReadUInt16(superblock, 20, littleEndian: true);
        var blockLog = ReadUInt16(superblock, 22, littleEndian: true);
        var idCount = ReadUInt16(superblock, 26, littleEndian: true);
        var majorVersion = ReadUInt16(superblock, 28, littleEndian: true);
        var minorVersion = ReadUInt16(superblock, 30, littleEndian: true);
        var bytesUsed = ReadUInt64(superblock, 40, littleEndian: true);
        var payloadLength = checked((ulong)(stream.Length - layout.PayloadOffset));

        if (!superblock[..4].SequenceEqual("hsqs"u8)
            || inodeCount == 0
            || blockSize < 4096
            || blockLog >= 32
            || blockSize != 1u << blockLog
            || compressionId == 0
            || idCount == 0
            || majorVersion != 4
            || minorVersion != 0
            || bytesUsed < SquashFsSuperblockLength
            || bytesUsed > payloadLength)
        {
            throw new InvalidDataException(
                $"'{appImagePath}' has an invalid SquashFS AppImage payload.");
        }

        return layout;
    }

    internal static async Task InstallAsync(
        string packagePath,
        string appImagePath,
        string expectedVersion,
        string expectedChannel,
        string expectedRuntimeIdentifier,
        CancellationToken cancellationToken = default,
        Func<string, long, CancellationToken, Task>? appImageRuntimeVerifier = null)
    {
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("The downloaded ABP Studio package was not found.", packagePath);
        }

        if (!File.Exists(appImagePath))
        {
            throw new FileNotFoundException("The installed ABP Studio AppImage was not found.", appImagePath);
        }

        var targetDirectory = Path.GetDirectoryName(appImagePath)
            ?? throw new InvalidOperationException($"Could not determine the directory containing '{appImagePath}'.");
        var stagedPath = Path.Combine(
            targetDirectory,
            $".{Path.GetFileName(appImagePath)}.{Guid.NewGuid():N}.abpdev-stage");

        try
        {
            using var installationLock = AcquireInstallLock(targetDirectory);
            var targetArchitecture = ValidateAppImage(appImagePath);
            var originalTarget = new FileInfo(appImagePath);
            var originalLength = originalTarget.Length;
            var originalLastWriteTimeUtc = originalTarget.LastWriteTimeUtc;
            var originalMode = OperatingSystem.IsWindows()
                ? (UnixFileMode?)null
                : File.GetUnixFileMode(appImagePath);

            using var archive = ZipFile.OpenRead(packagePath);
            ValidatePackageMetadata(
                archive,
                expectedVersion,
                expectedChannel,
                expectedRuntimeIdentifier,
                targetArchitecture);

            var appImageEntries = archive.Entries
                .Where(entry => string.Equals(entry.FullName, AppImageEntryName, StringComparison.Ordinal))
                .ToArray();

            if (appImageEntries.Length != 1 || appImageEntries[0].Length < MinimumAppImageLength)
            {
                throw new InvalidDataException(
                    $"Package '{packagePath}' must contain exactly one complete '{AppImageEntryName}' entry.");
            }

            await using (var input = appImageEntries[0].Open())
            await using (var output = new FileStream(
                stagedPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.SequentialScan))
            {
                await input.CopyToAsync(output, cancellationToken);
                await output.FlushAsync(cancellationToken);
                output.Flush(flushToDisk: true);

                if (output.Length != appImageEntries[0].Length)
                {
                    throw new InvalidDataException(
                        $"Extracted AppImage length {output.Length} does not match package entry length {appImageEntries[0].Length}.");
                }
            }

            var stagedLayout = ValidateAppImageLayout(stagedPath);
            if (stagedLayout.Architecture != targetArchitecture)
            {
                throw new InvalidDataException(
                    $"The package contains a {stagedLayout.Architecture} AppImage, but the installed AppImage is {targetArchitecture}.");
            }

            if (!OperatingSystem.IsWindows() && originalMode.HasValue)
            {
                File.SetUnixFileMode(stagedPath, originalMode.Value | UnixFileMode.UserExecute);
            }

            await (appImageRuntimeVerifier ?? VerifyAppImageRuntimeAsync)(
                stagedPath,
                stagedLayout.PayloadOffset,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            originalTarget.Refresh();
            if (!originalTarget.Exists
                || originalTarget.Length != originalLength
                || originalTarget.LastWriteTimeUtc != originalLastWriteTimeUtc)
            {
                throw new IOException(
                    $"The installed ABP Studio AppImage at '{appImagePath}' changed while the package was being prepared. Retry the switch.");
            }

            File.Move(stagedPath, appImagePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(stagedPath))
            {
                File.Delete(stagedPath);
            }
        }
    }

    private static string ResolveExplicitPath(string configuredPath)
    {
        var fullPath = Path.GetFullPath(configuredPath);
        if (File.Exists(fullPath))
        {
            ValidateAppImage(fullPath);
            return ResolveFinalLinkTarget(fullPath);
        }

        if (!Directory.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"The configured ABP Studio AppImage or installation directory '{fullPath}' does not exist.",
                fullPath);
        }

        foreach (var exactName in new[] { "AbpStudio.AppImage", "AbpStudio-stable.AppImage" })
        {
            var exactPath = Path.Combine(fullPath, exactName);
            if (File.Exists(exactPath))
            {
                ValidateAppImage(exactPath);
                return ResolveFinalLinkTarget(exactPath);
            }
        }

        var candidates = Directory
            .EnumerateFiles(fullPath, "AbpStudio*.AppImage", SearchOption.TopDirectoryOnly)
            .ToArray();

        if (candidates.Length == 1)
        {
            ValidateAppImage(candidates[0]);
            return ResolveFinalLinkTarget(candidates[0]);
        }

        var detail = candidates.Length == 0 ? "no matching AppImage was found" : "multiple matching AppImages were found";
        throw new InvalidOperationException(
            $"Could not select an ABP Studio AppImage in '{fullPath}': {detail}. Pass the exact AppImage path with --install-dir.");
    }

    private static bool TryResolveCandidate(
        string? candidate,
        bool requireAbpStudioFileName,
        out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(candidate);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (!File.Exists(fullPath))
        {
            return false;
        }

        var fileName = Path.GetFileName(fullPath);
        if (requireAbpStudioFileName
            && (!fileName.StartsWith("AbpStudio", StringComparison.OrdinalIgnoreCase)
                || !fileName.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        try
        {
            ValidateAppImage(fullPath);
            resolvedPath = ResolveFinalLinkTarget(fullPath);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private static string ResolveFinalLinkTarget(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var target = new FileInfo(fullPath).ResolveLinkTarget(returnFinalTarget: true);
        return target is null ? fullPath : Path.GetFullPath(target.FullName);
    }

    private static FileStream AcquireInstallLock(string targetDirectory)
    {
        var lockPath = Path.Combine(targetDirectory, InstallLockFileName);
        try
        {
            return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException(
                $"Another ABP Studio switch is already in progress for '{targetDirectory}'.",
                exception);
        }
    }

    private static Task VerifyAppImageRuntimeAsync(
        string appImagePath,
        long expectedPayloadOffset,
        CancellationToken cancellationToken)
    {
        return VerifyAppImageRuntimeAsync(
            appImagePath,
            expectedPayloadOffset,
            cancellationToken,
            TimeSpan.FromSeconds(10));
    }

    internal static async Task VerifyAppImageRuntimeAsync(
        string appImagePath,
        long expectedPayloadOffset,
        CancellationToken cancellationToken,
        TimeSpan timeoutDuration)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        if (timeoutDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutDuration));
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = appImagePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--appimage-offset");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start the staged AppImage at '{appImagePath}'.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(timeoutDuration);
        var outputTask = ReadBoundedRuntimeOutputAsync(process.StandardOutput, timeout.Token);
        var errorTask = ReadBoundedRuntimeOutputAsync(process.StandardError, timeout.Token);

        try
        {
            await Task.WhenAll(
                process.WaitForExitAsync(timeout.Token),
                outputTask,
                errorTask);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);

            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException($"The staged AppImage at '{appImagePath}' did not respond to its runtime check.");
        }
        catch
        {
            TryKill(process);
            throw;
        }

        var output = outputTask.Result.Trim();
        var error = errorTask.Result.Trim();
        if (process.ExitCode != 0
            || !long.TryParse(output, NumberStyles.None, CultureInfo.InvariantCulture, out var reportedOffset)
            || reportedOffset != expectedPayloadOffset)
        {
            throw new InvalidDataException(
                $"The staged AppImage runtime check failed with exit code {process.ExitCode}. "
                + $"Expected payload offset {expectedPayloadOffset}, received '{output}'."
                + (error.Length == 0 ? string.Empty : $" Error: {error}"));
        }
    }

    private static async Task<string> ReadBoundedRuntimeOutputAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        var output = new StringBuilder(MaximumRuntimeCheckOutputLength);
        var buffer = new char[64];

        while (true)
        {
            var charactersRead = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (charactersRead == 0)
            {
                return output.ToString();
            }

            for (var index = 0; index < charactersRead; index++)
            {
                var character = buffer[index];
                if (character == '\n')
                {
                    return output.ToString();
                }

                if (character != '\r')
                {
                    output.Append(character);
                }

                if (output.Length > MaximumRuntimeCheckOutputLength)
                {
                    throw new InvalidDataException("The staged AppImage runtime check produced too much output.");
                }
            }
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // The process exited between the state check and the kill request.
        }
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> buffer, int offset, bool littleEndian)
    {
        return littleEndian
            ? (ushort)(buffer[offset] | buffer[offset + 1] << 8)
            : (ushort)(buffer[offset] << 8 | buffer[offset + 1]);
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> buffer, int offset, bool littleEndian)
    {
        uint value = 0;
        for (var index = 0; index < sizeof(uint); index++)
        {
            var sourceIndex = littleEndian ? offset + sizeof(uint) - index - 1 : offset + index;
            value = value << 8 | buffer[sourceIndex];
        }

        return value;
    }

    private static ulong ReadUInt64(ReadOnlySpan<byte> buffer, int offset, bool littleEndian)
    {
        ulong value = 0;
        for (var index = 0; index < sizeof(ulong); index++)
        {
            var sourceIndex = littleEndian ? offset + sizeof(ulong) - index - 1 : offset + index;
            value = value << 8 | buffer[sourceIndex];
        }

        return value;
    }

    private static string? ParseFirstDesktopExecToken(string execValue)
    {
        var token = new StringBuilder();
        var inQuotes = false;
        var escaped = false;
        var started = false;

        foreach (var character in execValue.TrimStart())
        {
            if (escaped)
            {
                token.Append(character == 's' ? ' ' : character);
                escaped = false;
                started = true;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
                started = true;
                continue;
            }

            if (character == '"')
            {
                inQuotes = !inQuotes;
                started = true;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                if (started)
                {
                    break;
                }

                continue;
            }

            token.Append(character);
            started = true;
        }

        if (inQuotes || escaped || token.Length == 0)
        {
            return null;
        }

        return token.ToString();
    }

    private static void ValidatePackageMetadata(
        ZipArchive archive,
        string expectedVersion,
        string expectedChannel,
        string expectedRuntimeIdentifier,
        Architecture expectedArchitecture)
    {
        var nuspecEntries = archive.Entries
            .Where(entry => string.Equals(entry.FullName, NuspecEntryName, StringComparison.Ordinal))
            .ToArray();

        if (nuspecEntries.Length != 1)
        {
            throw new InvalidDataException(
                $"The package must contain exactly one '{NuspecEntryName}' entry.");
        }

        XDocument document;
        using (var nuspecStream = nuspecEntries[0].Open())
        using (var xmlReader = XmlReader.Create(
                   nuspecStream,
                   new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
        {
            document = XDocument.Load(xmlReader, LoadOptions.None);
        }

        var metadata = document.Descendants().SingleOrDefault(element => element.Name.LocalName == "metadata")
            ?? throw new InvalidDataException("The ABP Studio package has no metadata element.");

        ValidateMetadataValue(metadata, "id", "AbpStudio");
        ValidateMetadataValue(metadata, "version", expectedVersion);
        ValidateMetadataValue(metadata, "channel", expectedChannel);
        ValidateMetadataValue(metadata, "os", "linux");
        ValidateMetadataValue(metadata, "rid", expectedRuntimeIdentifier);
        ValidateMetadataValue(
            metadata,
            "machineArchitecture",
            expectedArchitecture == Architecture.Arm64 ? "arm64" : "x64");
    }

    private static void ValidateMetadataValue(XElement metadata, string elementName, string expectedValue)
    {
        var actualValue = metadata.Elements().SingleOrDefault(element => element.Name.LocalName == elementName)?.Value;
        if (!string.Equals(actualValue, expectedValue, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"The package metadata '{elementName}' value is '{actualValue ?? "<missing>"}', expected '{expectedValue}'.");
        }
    }

    private readonly record struct AppImageLayout(Architecture Architecture, long PayloadOffset);
}
