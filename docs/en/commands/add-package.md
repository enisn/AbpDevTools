---
id: add-package-command
title: Add Package Command
---

# Add Package Command

The `abpvdev add-package` command adds a NuGet package to your project and automatically configures the module dependency. It works with **any NuGet source**, unlike the official `abp add-package` command which only works with the official ABP package registry.

## The Problem with `abp add-package`

The official ABP CLI `abp add-package` command doesn't work with third-party packages:

```powershell
PS C:\MySolution> abp add-package AbpDev.QoL.Mvc.DataTables
Error: Could not find package 'AbpDev.QoL.Mvc.DataTables' in the 'https://abp.io/api/nmpkg' NuGet source.
```

This is because `abp add-package` queries ABP's internal package database instead of directly using the NuGet API. Any package not registered in their database will fail, even if it exists on NuGet.

## The Solution: AbpDevTools `add-package`

AbpDevTools `add-package` works differently:

- **NuGet source agnostic** - Uses `dotnet add package` directly, works with any NuGet source configured in nuget.config
- **Auto-configures DependsOn** - Automatically finds the module class and adds the dependency attribute
- **No assembly loading** - Uses metadata reflection to find module classes safely
- **Multi-project support** - Can add packages to all projects in a solution

## Usage

```
abpvdev add-package <packagename> [options]
```

## Parameters

| Parameter | Description |
|-----------|-------------|
| `packagename` | Name of the NuGet package to add |

## Options

| Option | Shortcut | Description |
|--------|----------|-------------|
| `--project` | `-p` | Path to the project file (.csproj). Default: searches current directory |
| `--version` | `-v` | Version of the package to install |
| `--skip-dependency` | `-s` | Skip adding module dependency (DependsOn attribute) |
| `--no-restore` | | Skip restoring the project after adding the package |
| `--all` | `-a` | Add package to all projects in the solution/folder |
| `--help` | `-h` | Shows help text |

## Examples

### Add Package

```bash
abpvdev add-package AbpDev.QoL.Mvc.DataTables
```

### Add Specific Version

```bash
abpvdev add-package AbpDev.QoL.Mvc.DataTables -v 1.0.0
```

### Add to All Projects

```bash
abpvdev add-package AbpDev.QoL.Mvc.DataTables -a
```

### Add to Specific Project

```bash
abpvdev add-package AbpDev.QoL.Mvc.DataTables -p C:\Path\To\Project.csproj
```

### Skip Dependency Configuration

```bash
abpvdev add-package Some.Other.Package -s
```

This is useful when adding packages that are not ABP modules.

### Skip Package Restore

```bash
abpvdev add-package AbpDev.QoL.Mvc.DataTables --no-restore
```

Useful when you want to defer restore or are in an offline environment.

## How It Works

The add-package command follows a 5-step process to add a package and configure its module dependency:

### Step 1: Add Package

Uses `dotnet add package` to add the NuGet package to the project file. This works with any NuGet source configured in your nuget.config.

```csharp
dotnet add "MyProject.csproj" package AbpDev.QoL.Mvc.DataTables --version 1.0.0
```

### Step 2: Restore Packages

Restores the package and its dependencies to make the DLLs available for analysis.

```csharp
dotnet restore "MyProject.csproj"
```

### Step 3: Find Module Using Metadata Reflection

Searches the NuGet package cache for DLLs matching the package name and uses metadata reflection (not assembly loading) to find classes that inherit from `AbpModule`.

This approach is safe and won't trigger any static constructors or module initializers.

### Step 4: Find Project Module Using Roslyn

Uses Roslyn to parse the project's source code and find the ABP module class that should depend on the new package.

### Step 5: Configure Dependency

Adds the `DependsOn` attribute to the project's module class:

```csharp
[DependsOn(typeof(DataTablesModule))]
public class MyProjectModule : AbpModule
{
    // ...
}
```

## Troubleshooting

### No ABP Module Class Found

If your project doesn't contain an ABP module class, the command will skip dependency configuration with a warning.

### Multiple Module Classes Found

If multiple module classes are found in the project, the command will use the first one and warn you about it.

### Package Not an ABP Module

If the added package doesn't contain an ABP module class (i.e., it's a regular library), the command will skip dependency configuration with a warning.

### Could Not Find Package DLLs

Make sure the NuGet cache is properly configured. The command searches:
- User's NuGet packages folder (`~/.nuget/packages`)
- Custom `NUGET_PACKAGES` environment variable path

## Comparison

| Feature | `abp add-package` | `abpvdev add-package` |
|---------|-------------------|------------------------|
| Official ABP packages | Yes | Yes |
| Third-party NuGet packages | No | Yes |
| Custom NuGet sources | No | Yes |
| Auto-configure DependsOn | Yes | Yes |
| Multi-project support | No | Yes |
| Works offline | No | Yes |
