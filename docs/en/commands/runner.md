---
id: runner-commands
title: Centralized Runner Commands
---

# Centralized Runner Commands

`abpdev run` delegates application ownership and output capture to one per-user background runner. The runner starts on demand and exits shortly after the last managed application stops. Normal users do not need to start or stop the runner itself.

## List Managed Applications

```bash
abpdev ps
```

`ps` lists active applications from every working-directory context. Useful options:

| Option | Description |
|--------|-------------|
| `<workingdirectory>` | Limit the list to this exact run context |
| `--current` | Limit the list to the current directory/YAML context |
| `--all`, `-a` | Include stopped, failed, and exited applications while the runner remains active |
| `--yml <path>` | Use an explicit YAML path when resolving a scoped context |
| `--json` | Emit machine-readable state |

Examples:

```bash
abpdev ps
abpdev ps --current
abpdev ps C:\Projects\MyApp --json
```

## Stop a Context

```bash
abpdev stop [workingdirectory]
```

With no directory, `stop` resolves the current directory and nearest `abpdev.yml`, exactly as `run` does. It never stops applications from another context.

Use `-p`/`--projects` to stop only matching application names, paths, display names, or npm scripts:

```bash
abpdev stop -p MyApp.Web
abpdev stop C:\Projects\MyApp -p api -p web
```

Without `-p`, every active application in the resolved context is stopped.

## Attach to the Dashboard

```bash
abpdev attach [workingdirectory]
```

`attach` opens the same state-and-log dashboard used by a foreground `abpdev run`. If the current directory does not match and only one context is active, that context is selected automatically. With multiple active contexts, an interactive terminal offers a context picker.

The dashboard clears the terminal once when it opens. Its status table remains at the top, while the log panel is constrained to the rows left in the current viewport and recalculated when the terminal size changes.

Press `L` to open a full-page streaming log viewer for the currently selected application, or for all applications when all-log mode is active. Use the arrow keys or `J`/`K` to scroll, `Page Up`/`Page Down` to move by a page, `Home`/`End` to jump to the oldest or newest output, and `F` to resume following new output. Press `L`, `Q`, or `Esc` to return to the dashboard without detaching.

Quitting or pressing `Ctrl+C` from `attach` only disconnects the dashboard; it does not stop applications. Use `S`, `Ctrl+S`, or `abpdev stop` for explicit process control.

## Context and Reconciliation Rules

- A context is the canonical working directory plus its resolved root YAML path.
- Project selectors are applied before the immutable launch plan is sent to the runner.
- Starting the same application again does not create a duplicate process.
- Active applications keep their original launch plan when YAML or CLI options change. Stop and run them again to apply changes.
- Stopping a context leaves other directories and YAML contexts untouched.

Runner communication uses a current-user-only named pipe and a per-user authentication token. Captured logs are bounded in memory and persisted with size-based rotation.
