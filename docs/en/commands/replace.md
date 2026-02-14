---
id: replace-command
title: Replace Command
---

# Replace Command

The `abpvdev replace` command replaces specified text in files under the current directory recursively. It's primarily used to replace connection strings in `appsettings.json` files but can be used for any text replacement.

## Usage

```
abpvdev replace <replacementconfigname> [options]
abpvdev replace [command] [...]
```

## Parameters

| Parameter | Description |
|-----------|-------------|
| `replacementconfigname` | Name of the replacement configuration to use, or 'all' to execute all |

## Options

| Option | Shortcut | Description |
|--------|----------|-------------|
| `--path` | `-p` | Working directory. Default: `.` |
| `--interactive` | `-i` | Interactive mode - asks to pick a config |
| `--help` | `-h` | Shows help text |

## Commands

### config

Manages replacement configuration. Use `abpvdev replace config` to open the configuration file.

## Examples

### Replace Connection Strings

```bash
abpvdev replace ConnectionStrings
```

Executes the replacement using the "ConnectionStrings" configuration.

### Run All Replacements

```bash
abpvdev replace all
```

Executes all configured replacements.

### Interactive Mode

```bash
abpvdev replace -i
```

Opens a prompt to select which configuration to use.

### Configure Replacements

```bash
abpvdev replace config
```

Opens the replacement configuration file for editing.

## Configuration

The replacement configuration is stored in `abpvdev.yml` under the `replacement` section.

### Default Configuration

```json
{
  "ConnectionStrings": {
    "FilePattern": "appsettings.json",
    "Find": "Trusted_Connection=True;",
    "Replace": "User ID=SA;Password=12345678Aa;"
  }
}
```

### Configuration Properties

| Property | Description |
|----------|-------------|
| `FilePattern` | Glob pattern to match files |
| `Find` | Text to find |
| `Replace` | Text to replace with |

### Advanced Configuration

```json
{
  "ConnectionStrings": {
    "FilePattern": "appsettings*.json",
    "Find": "Server=localhost",
    "Replace": "Server=myserver"
  },
  "ApiKeys": {
    "FilePattern": "*.config",
    "Find": "api-key-placeholder",
    "Replace": "actual-api-key"
  }
}
```

## Troubleshooting

### Files Not Found

Check that the FilePattern matches your files. Use wildcard patterns like `*.json`.

### No Changes Made

Verify the Find text exactly matches what's in your files. Text is case-sensitive.

### Safety First

The replace command modifies files. It's recommended to:
- Use version control
- Test with a dry run first
- Back up important files
