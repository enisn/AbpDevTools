---
id: switch-abp-studio-version
title: Switch ABP Studio Version
---

# Switch ABP Studio Version

The `abpvdev abp-studio switch` command allows you to switch the locally installed ABP Studio to any published version or channel. This is useful when you need to work with specific ABP Studio versions.

## Usage

```
abpvdev abp-studio switch <version> [options]
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
| `--install-dir` | `-i` | Custom install directory |
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
abpvdev abp-studio switch 2.0.1
```

### Switch to Beta Channel

```bash
abpvdev abp-studio switch 1.1.0 -c beta
```

### Force Redownload

```bash
abpvdev abp-studio switch 0.9.0 -f
```

### Use Custom Cache Directory

```bash
abpvdev abp-studio switch 1.0.0 -p D:\abp-studio-cache
```

This is useful for:
- Faster switching between versions (only apply step needed)
- Sharing cache across machines
- Using a fast SSD for downloads

## How It Works

The command performs these steps:

### 1. Detect Platform

Detects your OS and CPU architecture:
- Windows x64
- Windows ARM
- macOS Intel
- macOS ARM
- Linux

### 2. Prepare Directories

Creates or uses the specified directories:
- **Install directory**: Where ABP Studio is installed
  - Windows: `%LOCALAPPDATA%\abp-studio`
  - macOS: `~/Applications`
- **Packages directory**: Where packages are cached

### 3. Download Package

Downloads `abp-studio-{version}-{channel}-full.nupkg` with progress streaming.

### 4. Verify Updater

Checks that the platform updater exists:
- Windows: `Update.exe`
- macOS: `UpdateMac`

### 5. Apply Package

Runs the updater with `apply --package <path>` to install the version.

## Use Cases

### Specific Project Requirements

Some projects require specific ABP Studio versions:

```bash
abpvdev abp-studio switch 1.5.0
```

### Create Project with Specific Version

When you need to create a new project with an older version:

```bash
abpvdev abp-studio switch 0.8.0
abp new MyProject -v 0.8.0
```

### Rollback

If you encounter issues with a newer version:

```bash
abpvdev abp-studio switch 1.0.0
```

## Important Notes

### Does NOT Install First Time

This command doesn't install ABP Studio for the first time. Use the official installer for initial installation.

### Only Official Packages

The command only applies **official** ABP Studio NuGet packages. It doesn't add custom DLLs or executables.

### Shared Cache

Using a shared packages directory makes switching nearly instant:

```bash
# First time (download + apply)
abpvdev abp-studio switch 2.0.0 -p D:\abp-studio-cache

# Second time (only apply - much faster)
abpvdev abp-studio switch 1.9.0 -p D:\abp-studio-cache
```

## Troubleshooting

### Updater Not Found

Make sure ABP Studio is installed first using the official installer.

### Download Failed

Check your internet connection and try again with `--force`.

### Installation Failed

Check that you have write permissions to the install directory.

### Version Not Found

Verify the version exists in the specified channel:

```bash
abpvdev abp-studio switch 1.0.0 -c beta
```

## Next Steps

- [Bundle Commands](bundle-commands.md) - Managing ABP bundles
