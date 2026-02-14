---
id: bundle-commands
title: Bundle Commands
---

# Bundle Commands

AbpDevTools provides commands for managing ABP bundles, including listing and installing bundles.

## Commands Overview

| Command | Description |
|---------|-------------|
| `abpvdev bundle list` | Lists available ABP bundles |
| `abpvdev bundle install` | Installs ABP bundles |

## List Bundles

Shows all available ABP bundles that can be installed.

### Usage

```
abpvdev bundle list [options]
```

### Options

| Option | Shortcut | Description |
|--------|----------|-------------|
| `--help` | `-h` | Shows help text |

### Example

```bash
abpvdev bundle list
```

Output might show:

```
Available bundles:
- Angular
- Blazor
- MVC
- MAUI
- React
- Vue
```

## Install Bundles

Installs ABP client-side bundles.

### Usage

```
abpvdev bundle install [options]
```

or use the shortcut:

```
abpvdev bundle [options]
```

### Options

| Option | Shortcut | Description |
|--------|----------|-------------|
| `--skip-compilation` | | Skip application compilation |
| `--help` | `-h` | Shows help text |

### Examples

### Install All Bundles

```bash
abpvdev bundle install
```

### Install Without Compilation

```bash
abpvdev bundle install --skip-compilation
```

### Using Shortcut

```bash
abpvdev bundle
```

## Bundle Types

ABP supports various UI frameworks:

### Web Applications

- **MVC/Razor Pages**: Traditional ASP.NET Core MVC
- **Angular**: Single Page Application with Angular
- **React**: Single Page Application with React
- **Vue**: Single Page Application with Vue.js

### Mobile Applications

- **MAUI**: .NET Multi-platform App UI
- **React Native**: Cross-platform with React

### Blazor

- **Blazor WebAssembly**: Client-side Blazor
- **Blazor Server**: Server-side Blazor

## Integration with Run Command

The bundle install is automatically run as part of the prepare process:

```bash
abpvdev prepare
```

This ensures all client-side resources are properly bundled before running the application.

## Common Issues

### Bundle Not Found

Ensure you're in a project directory with an ABP application.

### Installation Fails

- Check Node.js is installed
- Verify npm packages can be downloaded
- Check network connectivity

## Related Commands

- [abpvdev prepare](../commands/prepare.md) - Prepares project including bundle installation
- [abpvdev run](../commands/run.md) - Run with `--install-libs` to install libraries
