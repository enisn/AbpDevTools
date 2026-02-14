---
id: prepare-command
title: Prepare Command
---

# Prepare Command

The `abpvdev prepare` command prepares your ABP project for first-time running on your machine. It automatically detects project dependencies, starts required environment apps, installs ABP libraries, and creates local configuration files.

## Usage

```
abpvdev prepare <workingdirectory> [options]
```

## Parameters

| Parameter | Description |
|-----------|-------------|
| `workingdirectory` | Working directory to prepare. Default: `.` (Current Directory) |

## Options

| Option | Shortcut | Description |
|--------|----------|-------------|
| `--no-config` | | Do not create local configuration file (abpvdev.yml) |
| `--help` | `-h` | Shows help text |

## Examples

### Prepare Current Directory

```bash
abpvdev prepare
```

### Prepare Specific Path

```bash
abpvdev prepare C:\Path\To\Projects
```

### Prepare Without Configuration

```bash
abpvdev prepare --no-config
```

Use this when you don't want to create local configuration files.

## What It Does

The prepare command performs these operations:

### 1. Dependency Analysis

Scans your projects to detect database and messaging dependencies:
- SQL Server
- PostgreSQL
- MySQL
- MongoDB
- Redis
- RabbitMQ
- Kafka

### 2. Environment Apps

Starts required Docker containers based on detected dependencies:
- SQL Server containers
- MongoDB containers
- Redis containers
- And more...

### 3. Library Installation

Runs `abp install-libs` to install client-side libraries (Angular/Blazor resources).

### 4. Blazor Bundling

Bundles Blazor WASM projects if detected.

### 5. Configuration

Creates `abpvdev.yml` files with appropriate environment settings.

## Configuration File

After running prepare, you'll have an `abpvdev.yml` file in your project directory. This file contains:
- Database connection strings
- Environment variables
- Custom settings

### Placeholders

The configuration supports these placeholders:

| Placeholder | Description |
|-------------|-------------|
| `{AppName}` | Application name (folder name if not detected) |
| `{Today}` | Current date (useful for separate databases per day) |

Example:
```json
{
  "ConnectionStrings__Default": "Server=localhost;Database={AppName}_{Today};User ID=SA;Password=12345678Aa;"
}
```

## Troubleshooting

### Docker Not Running

Make sure Docker Desktop is running before using prepare command.

### Missing Dependencies

If certain dependencies aren't detected, you can manually configure them in the generated configuration file.

### Permission Errors

On Linux, you may need to run Docker with appropriate permissions.
