---
id: logs-command
title: Logs Command
---

# Logs Command

The `abpvdev logs` command finds a given project under the current directory and shows its logs.

## Usage

```
abpvdev logs <projectname> [options]
abpvdev logs [command] [...]
```

## Parameters

| Parameter | Description |
|-----------|-------------|
| `projectname` | Determines which project to show logs for |

## Options

| Option | Shortcut | Description |
|--------|----------|-------------|
| `--path` | `-p` | Working directory of the command. Default: `.` |
| `--interactive` | `-i` | Options will be asked as prompt |
| `--help` | `-h` | Shows help text |

## Commands

### clear

Clears the logs for a project.

## Examples

### Show Logs

```bash
abpvdev logs Web
```

Shows logs for the project containing "Web" in its name.

### Clear Logs

```bash
abpvdev logs clear -p Web
```

Clears logs for the Web project with confirmation.

### Force Clear Logs

```bash
abpvdev logs clear -p Web -f
```

Clears logs without asking for confirmation.

### Interactive Mode

```bash
abpvdev logs -i
```

Opens an interactive prompt to select the project.

## How It Works

1. Searches for projects matching the given name
2. Locates the log directory (typically in `logs/` folder)
3. Opens the log files in a viewer or clears them

## Troubleshooting

### Project Not Found

Make sure you're in the solution directory and the project name is correct.

### No Logs Found

Some projects may not have log directories. Check the project structure.
