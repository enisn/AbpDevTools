---
id: local-sources
title: Local Sources Configuration
---

# Local Sources Configuration

Configure local source mappings to easily switch between package references and local project references during development. This is especially useful when working with local forks or development versions of external packages.

## Configure Local Sources

### Using the Command

```bash
abpvdev local-sources
```

Or use the shortcut command:

```bash
abpvdev references config
```

### Configuration File

This command opens a YAML configuration file where you can define your local source mappings:

```yaml
abp:
  RemotePath: https://github.com/abpframework/abp.git
  Path: C:\github\abp
  Packages:
    - Volo.Abp.*
    - Volo.Abp.Core
other-lib:
  Path: C:\source\other-lib
  Packages:
    - MyOrg.OtherLib.*
```

## Configuration Properties

| Property | Description |
|----------|-------------|
| `RemotePath` | The remote repository URL (optional - for cloning if local doesn't exist) |
| `Path` | Local path where the source code is located. Descendants of this path will be scanned for project files |
| `Packages` | List of package patterns to match (supports wildcards with `*`) |

### Package Patterns

- **Exact match**: `Volo.Abp.AspNetCore.Mvc` matches only that exact package
- **Wildcard**: `Volo.Abp.*` matches all packages starting with `Volo.Abp.`

## Example Configurations

### ABP Framework

```yaml
abp:
  RemotePath: https://github.com/abpframework/abp.git
  Path: C:\github\abp
  Packages:
    - Volo.Abp.*
```

### Custom Libraries

```yaml
custom-modules:
  Path: C:\source\custom-modules
  Packages:
    - MyOrg.FeatureA.*
    - MyOrg.FeatureB
```

### Multiple Sources

```yaml
abp:
  Path: C:\github\abp
  Packages:
    - Volo.Abp.*

third-party:
  Path: C:\libs\third-party
  Packages:
    - Contoso.*
    - Microsoft.*
```

## Important Notes

### Order Matters

The order of sources in the configuration file matters. When switching references, the first matching source will be used for a package. This only matters if the same package exists in different sources.

### Path Scanning

The `Path` directory and all its subdirectories will be scanned for `.csproj` files that match the package patterns.

### Relative vs Absolute Paths

- **Absolute paths**: `C:\github\abp` - Recommended for consistency
- **Relative paths**: Relative to the configuration file location

## Next Steps

- [Switch to Local References](switch-to-local.md) - Convert package references to local projects
- [Switch to Package References](switch-to-package.md) - Convert back to packages
