---
id: bundle-commands
title: Bundle Commands
---

# Bundle Commands

> **Important**: This command is **NOT related to ABP Studio**. It is used for **Blazor WebAssembly (WASM) bundles** in your application.

AbpDevTools provides commands for managing Blazor WASM client-side bundles. These commands generate static files (CSS, JavaScript) based on [bundle contributors](https://docs.abp.io/en/abp/latest/UI/Blazor/Bundling) defined in your application.

## What It Does

The bundle commands generate client-side static resource files for Blazor WASM applications by:

- Reading **bundle contributors** defined in your Blazor project
- Processing stylesheets (CSS) and scripts (JavaScript) specified in the bundles
- Generating optimized static files in the application's `wwwroot` folder

This is different from ABP Studio's bundling system and specifically targets the Blazor WASM bundling mechanism used by ABP applications.

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

- [abpvdev prepare](prepare.md) - Prepares project including bundle installation
- [abpvdev run](run.md) - Run with `--install-libs` to install libraries
