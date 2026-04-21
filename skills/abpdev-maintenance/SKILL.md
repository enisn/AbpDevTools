---
name: abpdev-maintenance
description: >-
  Use AbpDevTools maintenance and utility commands such as clean, replace,
  tools/config, notifications, update, find-file, find-port, and ABP Studio
  switching. Use when the user wants repo cleanup, config-driven text
  replacement, process/port inspection, tool updates, or local tool-path setup.
---

# abpdev maintenance

Use this skill for utility and maintenance commands outside the main run/migration/reference flows.

## Covered commands

| Command | Purpose |
|---|---|
| `abpdev clean` | Delete `bin`, `obj`, `node_modules`, or configured folders recursively |
| `abpdev clean config` | Open clean configuration |
| `abpdev replace` | Run configured text replacements |
| `abpdev replace config` | Open replacement configuration |
| `abpdev logs clear` | Delete `logs.txt` for one or many projects |
| `abpdev tools` | Show configured external tool paths |
| `abpdev tools config` | Open tool path configuration |
| `abpdev enable-notifications` | Enable desktop notifications |
| `abpdev disable-notifications` | Disable desktop notifications |
| `abpdev update` | Check for AbpDevTools updates |
| `abpdev update --apply` | Self-update AbpDevTools |
| `abpdev find-file` | Search descendant or ascendant file paths by text |
| `abpdev find-port` | Find or kill processes using a port |
| `abpdev abp-studio switch` | Switch ABP Studio version/channel |

## Global config location

```text
%AppData%\abpdev
```

Useful files:

- `clean-configuration.yml`
- `replacements.yml`
- `tools-configuration.yml`
- `notifications.yml`

## clean

```bash
abpdev clean [working-directory]
abpdev clean -s
abpdev clean -i node_modules
```

Useful options:

- `-s`, `--soft-delete`: send folders to recycle bin instead of hard delete
- `-i`, `--ignore-path`: skip matching paths

Default folders come from `clean-configuration.yml` and include:

- `bin`
- `obj`
- `node_modules`

## replace

```bash
abpdev replace <rule-name> -p <working-directory>
abpdev replace all -p <working-directory>
abpdev replace -i
abpdev replace ConnectionStrings -f appsettings.json
```

Useful options:

- `-p`, `--path`: working directory
- `-i`, `--interactive`: prompt for rule and/or file selection
- `-f`, `--files`: restrict matching files by partial name

Replacement rules are stored in `replacements.yml`.

Example shape:

```yaml
ConnectionStrings:
  file-pattern: appsettings.json
  find: Trusted_Connection=True;
  replace: User ID=SA;Password=12345678Aa;
```

## tools

```bash
abpdev tools
abpdev tools config
```

This config stores executable names/paths such as:

- `dotnet`
- `abp`
- `powershell`
- `open`
- `terminal`

Use `abpdev tools config` if the user's shell, terminal, or ABP CLI binary lives in a non-default location.

## logs clear

```bash
abpdev logs clear -p MyApp.HttpApi.Host
abpdev logs clear -p all --force
abpdev logs clear -i
```

Useful options:

- `-p`, `--project`: select a project name, or `all`
- `-i`, `--interactive`: choose interactively
- `-f`, `--force`: delete without confirmation

Behavior:

- Finds runnable projects in the working directory
- Deletes `<project>/Logs/logs.txt` when present
- Can clear logs for every discovered project with `-p all`

## notifications

```bash
abpdev enable-notifications
abpdev disable-notifications
abpdev disable-notifications --uninstall
```

Notes:

- Supported on Windows and macOS
- On Windows, enabling notifications installs the `BurntToast` PowerShell module
- `--uninstall` removes `BurntToast` while disabling notifications

## update

```bash
abpdev update
abpdev update --apply
abpdev update --apply --yes
```

Notes:

- `abpdev update` checks for a newer version
- `--apply` launches `dotnet tool update -g AbpDevTools`
- `--yes` skips the confirmation prompt

## find-file

```bash
abpdev find-file appsettings.json
abpdev find-file appsettings.json C:\src\MyApp
abpdev find-file Directory.Build.props . -a
```

Notes:

- Default behavior searches descendants
- `-a`, `--ascendant` searches upward through parent folders instead

## find-port

```bash
abpdev find-port 44307
abpdev find-port 44307 --kill
```

Behavior:

- Shows processes using the port
- Can interactively inspect details, copy PID/path, open location, refresh, or kill
- `--kill` skips the menu and kills the found process(es) directly

## ABP Studio switching

```bash
abpdev abp-studio switch 1.2.3
abpdev abp-studio switch 1.2.3 -c stable
abpdev abp-studio switch 1.2.3 -f
```

Useful options:

- `-c`, `--channel`: release channel, defaults to `stable`
- `-f`, `--force`: re-download the package
- `-i`, `--install-dir`: override install directory
- `-p`, `--packages-dir`: override package cache directory

Behavior:

- Downloads the requested ABP Studio package from `abp.io`
- Uses `Update.exe apply --package <nupkg>` to switch versions

## Guidance for agents

- Use `clean -s` when the user wants safer cleanup.
- Use `replace config` before `replace` when the needed replacement rule does not already exist.
- Use `tools config` before troubleshooting command-not-found issues with `dotnet`, `abp`, or terminal launch behavior.
- Use `find-port` when a dev port is occupied instead of asking the user to inspect processes manually.
