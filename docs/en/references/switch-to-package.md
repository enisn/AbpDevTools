---
id: switch-to-package
title: Switch to Package References
---

# Switch to Package References

The `abpvdev references to-package` command converts local project references back to package references. This is useful when you want to switch from development mode (using local sources) back to using NuGet packages.

## Usage

```
abpvdev references to-package [workingdirectory] [options]
```

## Parameters

| Parameter | Description |
|-----------|-------------|
| `workingdirectory` | Working directory. Default: `.` |

## Options

| Option | Shortcut | Description |
|--------|----------|-------------|
| `--sources` | `-s` | Sources to switch to packages (default: all sources) |
| `--help` | `-h` | Shows help text |

## Examples

### Switch All to Packages

```bash
abpvdev references to-package
```

Converts all local project references back to package references.

### Switch Specific Sources

```bash
abpvdev references to-package --sources abp
```

Only switches packages matching the "abp" source configuration back to NuGet packages.

### Switch Multiple Sources

```bash
abpvdev references to-package --sources abp custom-libs
```

### Switch in Specific Directory

```bash
abpvdev references to-package C:\Path\To\Projects
```

## How It Works

The command performs these steps:

1. **Find Projects**: Finds all `.csproj` files in the working directory
2. **Identify Local References**: Identifies project references pointing to configured local sources
3. **Retrieve Versions**: Gets the backed-up version from PropertyGroup (stored during `to-local`)
4. **Convert References**: Replaces project references with package references

### If Version is Backed Up

The conversion is automatic:

```xml
<!-- Before (Project Reference) -->
<ProjectReference Include="..\..\abp\framework\Volo.Abp.AspNetCore.Mvc\Volo.Abp.AspNetCore.Mvc.csproj" />

<!-- After (Package Reference) -->
<PackageReference Include="Volo.Abp.AspNetCore.Mvc" Version="8.0.0" />
```

### If Version is NOT Backed Up

You'll be prompted to enter the version:

```
Enter version for Volo.Abp.AspNetCore.Mvc: _
```

## Workflow

Typical workflow for working with local sources:

1. **Start with packages**: Use NuGet packages in your project
2. **Switch to local**: `abpvdev references to-local` - for development
3. **Work on code**: Make changes in your local source
4. **Switch back**: `abpvdev references to-package` - when done

## Use Cases

### Done with Development

After debugging or adding features to a local fork:

```bash
abpvdev references to-package
```

### Switching Branches

When switching between branches that expect different versions:

```bash
abpvdev references to-package --sources abp
```

### CI/CD

Ensure package references are used in CI pipelines:

```bash
abpvdev references to-package
dotnet build
```

## Troubleshooting

### Version Not Found

If the backed-up version is missing, you'll be prompted to enter it manually. You can find the version in:
- NuGet.org
- Your local NuGet cache
- The project's csproj file

### Multiple Matches

If multiple package versions match, specify the exact version when prompted.

## Important Notes

- **Always backup**: The `to-local` command automatically backs up versions
- **Don't modify backups**: Keep the PropertyGroup intact for easy restoration
- **Order matters**: Run `to-package` before `to-local` again to avoid conflicts

## Next Steps

- [Configure Local Sources](local-sources.md) - Set up your sources
- [Switch to Local References](switch-to-local.md) - Convert packages to local projects
