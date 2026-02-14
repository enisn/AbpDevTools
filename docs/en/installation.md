---
id: installation
title: Installation Guide
---

# Installation

This guide covers how to install AbpDevTools on your system.

## Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) 6.0 or later
- Windows, macOS, or Linux

## Standard Installation

Install AbpDevTools as a global .NET tool using the following command:

```bash
dotnet tool update -g AbpDevTools
```

This will install the latest version of AbpDevTools and make the `abpvdev` command available system-wide.

## Version-Specific Installation

You can install a specific version of AbpDevTools targeting a specific .NET runtime:

### For .NET 8.0
```bash
dotnet tool update -g AbpDevTools --framework net8.0
```

### For .NET 9.0
```bash
dotnet tool update -g AbpDevTools --framework net9.0
```

### For .NET 6.0
```bash
dotnet tool update -g AbpDevTools --framework net6.0
```

## Local Installation

If you don't have access to the NuGet package source or want to install from source:

### Prerequisites for Local Installation
- PowerShell (Windows) or Bash (macOS/Linux)
- .NET SDK 10.0 or later

### Installation Steps

1. Clone the repository or navigate to the project directory
2. Run the install script:

```bash
pwsh install.ps1
```

or on Linux/macOS:

```bash
./install.sh
```

This will build the project and install it as a global tool.

## Verifying Installation

After installation, verify that AbpDevTools is working:

```bash
abpvdev --help
```

You should see the help message with all available commands.

## Updating AbpDevTools

To update to the latest version:

```bash
dotnet tool update -g AbpDevTools
```

## Uninstalling

To uninstall AbpDevTools:

```bash
dotnet tool uninstall -g AbpDevTools
```

## Troubleshooting

### Command Not Found

If `abpvdev` is not recognized after installation:

1. Close and reopen your terminal
2. Restart your computer if the issue persists
3. Check your PATH environment variable

### Permission Errors

On Linux/macOS, you may need to use `sudo` for global installation:

```bash
sudo dotnet tool install -g AbpDevTools
```

## Next Steps

- [Getting Started](getting-started.md) - Learn the basics
- [Commands](commands/build.md) - Explore available commands
