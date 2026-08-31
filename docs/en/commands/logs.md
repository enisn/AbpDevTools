---
id: logs-command
title: Logs Command
---

# Logs Command

The `abpdev logs` command shows stdout/stderr captured by the centralized runner when the project is managed, and otherwise falls back to the project's `Logs/logs.txt` file.

## Usage

```
abpdev logs <projectname> [options]
abpdev logs [command] [...]
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
| `--lines` | `-n` | Number of recent lines to print. Default: `100` |
| `--follow` | `-f` | Follow stdout/stderr captured by the centralized runner |
| `--managed` | | Force runner-captured logs; without a project, combine logs for the context |
| `--open` | `-o` | Open the application log file/folder instead of printing it |
| `--help` | `-h` | Shows help text |

## Commands

### clear

Clears the logs for a project.

## Examples

### Show Logs

```bash
abpdev logs Web
```

Shows logs for the project containing "Web" in its name.

### Follow Managed Output

```bash
abpdev logs Web --follow
```

This follows stdout and stderr without taking process ownership. `Ctrl+C` detaches from the log stream and leaves the application running.

### Combine Logs for the Current Context

```bash
abpdev logs --managed --lines 200
```

### Clear Logs

```bash
abpdev logs clear -p Web
```

Clears logs for the Web project with confirmation.

### Force Clear Logs

```bash
abpdev logs clear -p Web -f
```

Clears logs without asking for confirmation.

### Interactive Mode

```bash
abpdev logs -i
```

Opens an interactive prompt to select the project.

## How It Works

1. Resolves the same working-directory/YAML context used by `abpdev run`
2. Prefers captured runner output when the requested project is managed
3. Falls back to the application's `Logs/logs.txt` when no managed match exists

Runner output is also written to the per-user `abpdev/runner/logs` directory. Each file is rotated at 5 MB and one previous segment is retained.

## Troubleshooting

### Project Not Found

Make sure you're in the solution directory and the project name is correct.

### No Logs Found

Some projects may not have log directories. Check the project structure.
