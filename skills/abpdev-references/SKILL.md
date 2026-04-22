---
name: abpdev-references
description: >-
  Switch between NuGet package references and local project references using the
  abpdev CLI. Use when the user wants to configure local-sources.yml, convert
  packages to local project references, convert back to package references, debug
  ABP source locally, use the local-sources config alias, or troubleshoot
  reference switching in ABP-based projects.
---

# abpdev references

Switch `.csproj` files between NuGet `PackageReference` and local `ProjectReference` entries. This lets you work against local source checkouts (ABP Framework, custom modules, etc.) without publishing NuGet packages.

## Prerequisites

Install AbpDevTools as a global dotnet tool:

```bash
dotnet tool update -g AbpDevTools
```

## Commands

| Command | Purpose |
|---------|---------|
| `abpdev local-sources` | Open the same local sources configuration file directly |
| `abpdev references config` | Create (if missing) and open `local-sources.yml` in your default editor |
| `abpdev references to-local [dir] [-s sources]` | PackageReference -> ProjectReference |
| `abpdev references to-package [dir] [-s sources]` | ProjectReference -> PackageReference |

`abpdev local-sources` and `abpdev references config` point to the same global configuration.

- `dir` defaults to the current directory. All `.csproj` files are found recursively.
- `-s` / `--sources` filters by source key (space-separated). Omit to process all configured sources.

## Configuration

### File location

```
%AppData%\abpdev\local-sources.yml
```

Run `abpdev references config` or `abpdev local-sources` to create the file with defaults and open it.

### Format

YAML with **kebab-case** keys. Each top-level key is a **source key**.

```yaml
abp:
  remote-path: https://github.com/abpframework/abp.git
  branch: dev
  path: C:\github\abp
  packages:
    - Volo.*

my-modules:
  path: C:\source\my-modules
  packages:
    - MyOrg.ModuleA.*
    - MyOrg.ModuleB
```

> **Important:** Always use kebab-case keys (`remote-path`, `path`, `packages`). PascalCase keys like `RemotePath` will be silently ignored by the deserializer.

### Properties

| Key | Required | Description |
|-----|----------|-------------|
| `path` | Yes | Absolute path to local source root. All `.csproj` files under this directory are scanned recursively. |
| `remote-path` | No | Git clone URL. When `path` is missing/empty, the tool offers to clone from here. |
| `branch` | No | Branch to checkout when cloning from `remote-path`. |
| `packages` | Yes | Package name patterns to match against `PackageReference` entries in consumer projects. |

### Package patterns

- **Wildcard:** `Volo.*` -- matches any package starting with `Volo.` (prefix match, case-insensitive)
- **Exact:** `MyOrg.SpecificLib` -- matches only that exact name (case-insensitive)

### Matching rule

A `PackageReference` is converted only when **both** conditions are met:
1. The package name matches a `packages` pattern in a configured source.
2. A `.csproj` file whose **name (without extension) equals the package ID** exists under that source's `path`. Example: package `Volo.Abp.Core` requires `Volo.Abp.Core.csproj` somewhere under the source path.

### Default configuration

First run creates:

```yaml
abp:
  remote-path: https://github.com/abpframework/abp.git
  branch: dev
  path: C:\github\abp
  packages:
    - Volo.*
```

Edit the `path` value to point to your local ABP Framework checkout.

## Switching to local references

```bash
abpdev references to-local
```

What happens to each matching `.csproj`:

1. `PackageReference` is rewritten to `ProjectReference` with a relative path to the local `.csproj`.
2. `Version`, `PrivateAssets`, `IncludeAssets`, `ExcludeAssets` attributes are removed.
3. The original package version is backed up in a `PropertyGroup` as `<{sourceKey}Version>` (one version per source, not per package).

### Example diff

```diff
 <ItemGroup>
-  <PackageReference Include="Volo.Abp.AspNetCore.Mvc" Version="8.0.0" />
-  <PackageReference Include="Volo.Abp.Ddd.Application" Version="8.0.0" />
+  <ProjectReference Include="..\..\abp\framework\src\Volo.Abp.AspNetCore.Mvc\Volo.Abp.AspNetCore.Mvc.csproj" />
+  <ProjectReference Include="..\..\abp\framework\src\Volo.Abp.Ddd.Application\Volo.Abp.Ddd.Application.csproj" />
 </ItemGroup>

+<PropertyGroup>
+  <abpVersion>8.0.0</abpVersion>
+</PropertyGroup>
```

### Missing source directory

If a source `path` doesn't exist or is empty, you are prompted to:
- **Skip** the source and continue
- **Open configuration** to fix the path
- **Clone** from `remote-path` (if configured and git is available)

## Switching back to package references

```bash
abpdev references to-package
```

What happens:

1. Each `ProjectReference` pointing into a configured source is converted back to `PackageReference`.
2. The version is restored from the backed-up `<{sourceKey}Version>` property.
3. If no backup exists, you are prompted once per source: `Enter version for source '{key}':`.

### Version resolution order

1. `<{sourceKey}Version>` property in the csproj (written by `to-local`).
2. Version already prompted for this source key during the current run (cached).
3. Interactive prompt.

## Typical workflow

```bash
# 1. One-time setup: configure your local source paths
abpdev references config
# or
abpdev local-sources
# Edit the YAML: set `path` to your local checkout

# 2. Switch to local references for debugging / development
abpdev references to-local

# 3. Work with local source: set breakpoints, make changes...

# 4. Switch back to NuGet packages when done
abpdev references to-package

# 5. Restore and verify the build
dotnet restore && dotnet build
```

### Scoped switching

Only switch specific sources:

```bash
abpdev references to-local -s abp
abpdev references to-package -s abp my-modules
```

### Working in a specific directory

```bash
abpdev references to-local C:\Projects\MyAbpApp -s abp
```

## Multiple sources example

```yaml
abp:
  remote-path: https://github.com/abpframework/abp.git
  branch: dev
  path: C:\github\abp
  packages:
    - Volo.*

company-modules:
  path: C:\source\company-modules
  packages:
    - Acme.Modules.*

third-party:
  path: C:\libs\contoso
  packages:
    - Contoso.Payments
    - Contoso.Notifications
```

Source order matters when the same package appears in multiple sources -- first match wins.

## Troubleshooting

**Package not converted to local:**
- Verify a `.csproj` whose file name matches the package ID exists under the source `path`.
- Verify the package name matches at least one entry in `packages`.
- Run `abpdev references config` or `abpdev local-sources` and check the `path` value.

**Version not restored when switching back:**
- `to-local` stores one version per source key, not per package. All packages from the same source share that version.
- If the backup property is missing (e.g. you added `ProjectReference` entries manually), the tool will prompt for a version.

**Build errors after switching:**
- Run `dotnet restore` before building.

**Config keys silently ignored:**
- Use kebab-case (`remote-path`) not PascalCase (`RemotePath`) in `local-sources.yml`.
