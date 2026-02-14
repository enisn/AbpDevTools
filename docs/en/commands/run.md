---
id: run-command
title: Run Command
---

# Run Command

The `abpvdev run` command runs the solution in the current directory. It supports multiple solutions, multiple applications, and DbMigrator projects.

## Usage

```
abpvdev run <workingdirectory> [options]
```

## Parameters

| Parameter | Description |
|-----------|-------------|
| `workingdirectory` | Working directory to run. Default: `.` (Current Directory) |

## Options

| Option | Shortcut | Description |
|--------|----------|-------------|
| `--watch` | `-w` | Watch mode - automatically rebuild on file changes |
| `--skip-migrate` | | Skip migration and run projects directly |
| `--all` | `-a` | Run all projects without prompting |
| `--no-build` | | Skip build before running |
| `--install-libs` | `-i` | Run 'abp install-libs' while running |
| `--graphBuild` | `-g` | Use /graphBuild for building |
| `--projects` | `-p` | Names or part of names of projects to run |
| `--configuration` | `-c` | Build configuration (Debug/Release) |
| `--env` | `-e` | Virtual environment name |
| `--help` | `-h` | Shows help text |

## Examples

### Run in Current Directory

```bash
abpvdev run
```

### Run in Specific Path

```bash
abpvdev run C:\Path\To\Projects
```

### Run in Release Mode

```bash
abpvdev run -c Release
```

### Run All Projects Without Prompt

```bash
abpvdev run -a
```

### Skip Migration

```bash
abpvdev run --skip-migrate
```

Useful when you don't need to apply migrations.

### Watch Mode

```bash
abpvdev run -w
```

Automatically rebuilds and restarts when source files change.

> Note: In watch mode, URLs cannot be printed because dotnet doesn't provide output.

### Run with Virtual Environment

```bash
abpvdev run -e SqlServer
```

Uses the SqlServer virtual environment configuration.

### Run Specific Projects

```bash
abpvdev run -p MyApp.Web.HttpApi.Host
```

### Run with ABP Library Installation

```bash
abpvdev run --install-libs
```

Runs `abp install-libs` automatically while starting the application.

## Project Detection

AbpDevTools automatically detects these project types:

| Type | Detection | Default Behavior |
|------|-----------|------------------|
| Web Host | Contains "HttpApi.Host" | Runs as web application |
| Blazor WASM | Contains "Blazor" | Runs as Blazor client |
| MAUI | Contains "Maui" or "Mobile" | Runs as mobile app |
| DbMigrator | Contains "DbMigrator" | Runs before applications |

## Configuration

Use the `abpvdev run config` command to customize:
- Project name conventions
- Default options
- Environment variables

## Multiple Solutions

When multiple solutions exist in the directory:

```bash
abpvdev run
```

You'll be prompted to select which solution to run.

![Run Multiple Solutions](../images/abpdevrun-multiplesolutions.gif)

## Troubleshooting

### Application Doesn't Start

1. Check that all dependencies are installed: `abpvdev prepare`
2. Verify database connections in configuration
3. Check for port conflicts

### Port Already in Use

Use the `--projects` option to run specific projects, or stop other running applications.

### Missing Dependencies

Run `abpvdev prepare` to install all required dependencies.
