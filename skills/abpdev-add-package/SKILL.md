---
name: abpdev-add-package
description: >-
  Add NuGet packages with abpdev and automatically wire ABP module dependencies.
  Use when the user wants to install a package from any NuGet source, target one
  or many projects, control restore/version behavior, or troubleshoot DependsOn
  updates.
---

# abpdev add-package

Use `abpdev add-package` when a user wants ABP-aware package installation without the limitations of `abp add-package`.

## When to use

- Add a package from any NuGet source configured in `nuget.config`
- Add a package to one project or many projects
- Automatically add `[DependsOn(typeof(...))]` to the project's ABP module
- Troubleshoot package install or dependency-wiring failures

## Prerequisites

Install AbpDevTools as a global dotnet tool:

```bash
dotnet tool update -g AbpDevTools
```

## Command

```bash
abpdev add-package <package-name> [options]
```

## Main options

| Option | Meaning |
|---|---|
| `-p`, `--project` | Target a specific `.csproj` |
| `-v`, `--version` | Install a specific version |
| `--prerelease` | Allow prerelease versions |
| `-s`, `--skip-dependency` | Skip adding `DependsOn` |
| `--no-restore` | Skip restore after adding the package |
| `-a`, `--all` | Add to all discovered projects |

## Typical usage

```bash
abpdev add-package Volo.Abp.Autofac
abpdev add-package My.Company.Package -v 1.2.3
abpdev add-package My.Company.Package --prerelease
abpdev add-package My.Company.Package -p C:\src\MyApp\src\MyApp.HttpApi.Host\MyApp.HttpApi.Host.csproj
abpdev add-package Some.NonAbp.Package --skip-dependency
abpdev add-package My.Shared.Package --all
```

## What the command does

1. Runs `dotnet add package`, so normal NuGet sources are respected.
2. Restores packages unless `--no-restore` is passed.
3. Reads the installed package metadata to find the ABP module type.
4. Parses the consumer project source to find its ABP module class.
5. Adds the needed `[DependsOn(typeof(...))]` entry if applicable.

## Guidance for agents

- Prefer this command over `abp add-package` when the package may come from a custom/private feed.
- Use `--skip-dependency` for non-ABP packages or when the user only wants the package reference.
- Use `-p` when the repo contains multiple app/module projects and the target is known.
- Use `-a` only when the user explicitly wants every matching project updated.
- This command is designed to be safe to re-run and should avoid duplicate dependency attributes.

## Troubleshooting

**Package installed but no `DependsOn` change appeared**
- The package may not expose an ABP module class.
- The target project may not contain an ABP module class.
- Re-run with `--skip-dependency` if the package is not meant to be an ABP module.

**Package cannot be found**
- Check the configured NuGet sources in `nuget.config`.
- If a prerelease version is needed, add `--prerelease`.

**Wrong project was updated**
- Re-run with `-p <path-to-csproj>`.

**Restore is too slow or should be handled separately**
- Use `--no-restore`, then run `dotnet restore` later.
