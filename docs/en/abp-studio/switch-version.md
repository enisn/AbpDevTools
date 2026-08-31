---
id: switch-abp-studio-version
title: Switch ABP Studio Version
---

# Switch ABP Studio Version

The `abpdev abp-studio switch` command switches an existing ABP Studio installation to a published version and channel. Windows and macOS use the platform updater. Linux replaces the installed AppImage from the official package and requires an application restart.

## Usage

```
abpdev abp-studio switch <version> [options]
```

## Parameters

| Parameter | Description |
|-----------|-------------|
| `version` | Target ABP Studio version to install. Default: 1.0.0 |

## Options

| Option | Shortcut | Description |
|--------|----------|-------------|
| `--channel` | `-c` | Channel to download from. Default: "stable" |
| `--force` | `-f` | Forces re-download even if package exists |
| `--install-dir` | `-i` | Custom install directory. On Linux, pass the AppImage file or its containing directory |
| `--packages-dir` | `-p` | Custom cache directory for packages |
| `--help` | `-h` | Shows help text |

## Channels

| Channel | Description |
|---------|-------------|
| `stable` | Official stable releases |
| `beta` | Beta releases |
| `preview` | Preview releases |
| `nightly` | Nightly builds (unstable) |

## Examples

### Switch to Stable Version

```bash
abpdev abp-studio switch 2.0.1
```

### Switch to Beta Channel

```bash
abpdev abp-studio switch 1.1.0 -c beta
```

### Force Redownload

```bash
abpdev abp-studio switch 0.9.0 -f
```

### Use Custom Cache Directory

```bash
abpdev abp-studio switch 1.0.0 -p D:\abp-studio-cache
```

This is useful for:

- Faster switching between versions (only apply step needed)
- Sharing cache across machines
- Using a fast SSD for downloads

### Select a Linux AppImage

On Linux, `--install-dir` accepts either the exact AppImage path or a directory containing it:

```bash
abpdev abp-studio switch 3.0.10 --install-dir ~/Applications/AbpStudio.AppImage
```

When the option is omitted, the command searches in this order:

1. The `APPIMAGE` environment variable.
2. `abp-studio.desktop` in the configured XDG application directories.
3. `~/.local/opt/abp-studio/AbpStudio.AppImage`.
4. `~/Applications/AbpStudio.AppImage`.
5. `/opt/abp-studio/AbpStudio.AppImage`.

An explicit directory may contain `AbpStudio.AppImage`, `AbpStudio-stable.AppImage`, or one unambiguous `AbpStudio*.AppImage` file.

## Switch Workflow

The command performs these steps:

### 1. Detect Platform

Detects the OS and CPU architecture:

- Windows x64
- Windows ARM
- macOS Intel
- macOS ARM
- Linux x64, using release alias `linux`
- Linux ARM64, using release alias `linux-arm64`

### 2. Prepare Directories

Creates or uses the package cache directory. Linux uses `~/.abpdev/cache/AbpStudio/packages`; the complete default cache hierarchy is private to the current user. Use `--packages-dir` to override it, and only use a custom cache you trust.

On Windows, the default installation directory is `%LOCALAPPDATA%\abp-studio`. On macOS, the command uses `/Applications/ABP Studio.app` or `~/Applications/ABP Studio.app`. Linux uses the existing AppImage discovered above and does not create a new installation location.

### 3. Download Package

Downloads the full package with progress streaming:

- Linux: `AbpStudio-{version}-{channel}-full.nupkg`
- Windows and macOS: `abp-studio-{version}-{channel}-full.nupkg`

### 4. Apply the Package

On Linux, the command validates the package metadata and architecture, extracts `lib/app/AbpStudio.AppImage` to a staged file beside the installed AppImage, restores executable permissions, and atomically replaces the installed file. Close and reopen ABP Studio to run the selected version.

On Windows and macOS, the command verifies and runs the existing platform updater:

- Windows: `Update.exe apply --package <path>`
- macOS: `UpdateMac apply --package <path>`

## Use Cases

### Specific Project Requirements

Some projects require specific ABP Studio versions:

```bash
abpdev abp-studio switch 1.5.0
```

### Create Project with Specific Version

When you need to create a new project with an older version:

```bash
abpdev abp-studio switch 0.8.0
abp new MyProject -v 0.8.0
```

### Rollback

If you encounter issues with a newer version:

```bash
abpdev abp-studio switch 1.0.0
```

## Installation Constraints

### Existing Installation Required

This command doesn't install ABP Studio for the first time. Use the official installer for initial installation.

### Only Official Packages

The command only applies **official** ABP Studio NuGet packages. It doesn't add custom DLLs or executables.

### Shared Cache

Using a shared packages directory makes switching nearly instant:

```bash
# First time (download + apply)
abpdev abp-studio switch 2.0.0 -p D:\abp-studio-cache

# Second time (only apply - much faster)
abpdev abp-studio switch 1.9.0 -p D:\abp-studio-cache
```

## Troubleshooting

### Installation Not Found

Make sure ABP Studio is installed first using the official installer. On Linux, pass the AppImage path or its containing directory with `--install-dir` if automatic discovery cannot find it.

### Download Failed

Check your internet connection and try again with `--force`.

### Installation Failed

Check that you have write permissions to the install directory.

### Version Not Found

Verify the version exists in the specified channel:

```bash
abpdev abp-studio switch 1.0.0 -c beta
```

## Next Steps

- [Bundle Commands](bundle-commands.md) - Managing ABP bundles
