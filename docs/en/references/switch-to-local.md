---
id: switch-to-local
title: Switch to Local References
---

# Switch to Local References

The `abpvdev references to-local` command converts package references to local project references for development. This allows you to work with local source code instead of NuGet packages.

## Usage

```
abpvdev references to-local [workingdirectory] [options]
```

## Parameters

| Parameter | Description |
|-----------|-------------|
| `workingdirectory` | Working directory. Default: `.` |

## Options

| Option | Shortcut | Description |
|--------|----------|-------------|
| `--sources` | `-s` | Sources to switch to local (default: all sources) |
| `--help` | `-h` | Shows help text |

## Examples

### Switch All to Local

```bash
abpvdev references to-local
```

Converts all matching package references to local project references.

### Switch Specific Sources

```bash
abpvdev references to-local --sources abp
```

Only switches packages matching the "abp" source configuration.

### Switch Multiple Sources

```bash
abpvdev references to-local --sources abp custom-libs
```

### Switch in Specific Directory

```bash
abpvdev references to-local C:\Path\To\Projects
```

## How It Works

The command performs these steps:

1. **Find Projects**: Finds all `.csproj` files in the working directory
2. **Match Packages**: Matches package references against configured local source patterns
3. **Find Projects**: Locates matching `.csproj` files in the local source paths
4. **Convert References**: Replaces package references with project references using relative paths
5. **Backup Versions**: Stores original package versions in PropertyGroup for later restoration

### Before (Package Reference)

```xml
<PackageReference Include="Volo.Abp.AspNetCore.Mvc" Version="8.0.0" />
```

### After (Project Reference)

```xml
<ProjectReference Include="..\..\abp\framework\Volo.Abp.AspNetCore.Mvc\Volo.Abp.AspNetCore.Mvc.csproj" />
```

### Backup

Original versions are stored in the project file:

```xml
<PropertyGroup>
  <Backup_Volo_Abp_AspNetCore_Mvc>8.0.0</Backup_Volo_Abp_AspNetCore_Mvc>
</PropertyGroup>
```

## Use Cases

### Debugging ABP Issues

Switch to local ABP source to debug issues or understand internal behavior:

```bash
abpvdev references to-local --sources abp
```

### Developing Custom Modules

Work with your own fork of a package:

```bash
abpvdev references to-local --sources custom-modules
```

### Testing Changes Locally

See your changes reflected immediately without publishing NuGet packages.

## Troubleshooting

### Package Not Found

- Verify the local source path is correct
- Check package patterns match the expected packages
- Ensure project files exist in the local source directory

### Circular Dependencies

If you encounter circular dependencies, you may need to adjust your project structure.

### Build Errors After Switching

Restore packages first:
```bash
dotnet restore
```

## Next Steps

- [Configure Local Sources](local-sources.md) - Set up your sources
- [Switch to Package References](switch-to-package.md) - Convert back to packages
