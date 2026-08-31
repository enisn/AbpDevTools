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
abpdev stop --all [--force]
```

With no directory, `stop` first looks for the exact current directory/YAML context, then for a unique nearest active ancestor context. If neither matches, an interactive terminal asks which active context to stop. A non-interactive terminal exits with command help and copyable `abpdev stop <workingdirectory> [--yml <path>]` commands for every active context.

Passing a directory or `--yml` always targets that exact context and never falls back to another one.

Use `-p`/`--projects` to stop only matching application names, paths, display names, or npm scripts:

```bash
abpdev stop -p MyApp.Web
abpdev stop C:\Projects\MyApp -p api -p web
```

Without `-p`, every active application in the resolved context is stopped.

Use `--all`/`-a` to stop every active application across every runner context:

```bash
abpdev stop --all
abpdev stop --all --force
```

On an interactive terminal, `--all` displays a confirmation that defaults to No. `--force` bypasses that confirmation and is required in non-interactive environments. Calling `abpdev stop --all` without `--force` from CI, an agent, or redirected input exits with a non-zero status, prints command help, and shows the exact `abpdev stop --all --force` usage.

Because `--all` is global, it cannot be combined with a working directory, `--yml`, or `-p`/`--projects`. `--force` is valid only with `--all`.

## Attach to the Dashboard

```bash
abpdev attach [workingdirectory]
```

`attach` opens the same state-and-log dashboard used by a foreground `abpdev run`. It first selects the exact current context or a unique nearest active ancestor context. If neither matches and only one context is active, that context is selected automatically. With multiple active contexts, an interactive terminal offers a context picker.

`attach` requires an interactive terminal. When standard streams are redirected or a non-interactive environment is detected, it exits with a non-zero status instead of prompting or opening the dashboard. The error includes command help and points automation to bounded alternatives:

```bash
abpdev ps --json
abpdev logs --managed --path <working-directory> --lines 100
```

Detection recognizes `ABPDEV_NON_INTERACTIVE=1`, `ABPDEV_INTERACTIVE=0`, common `CI`/`NONINTERACTIVE` markers, `DEBIAN_FRONTEND=noninteractive`, and `TERM=dumb`. `ABPDEV_INTERACTIVE=1` can override ambient CI markers when the command still has a real terminal; redirected streams remain non-interactive.

The dashboard clears the terminal once when it opens. Its status table remains at the top, while the log panel is constrained to the rows left in the current viewport and recalculated when the terminal size changes.

Press `L` to leave the live dashboard temporarily and print plain, streaming logs for the currently selected application, or for all applications when all-log mode is active. The initial output is limited to the latest 1000 entries, after which new entries are appended without being retained by the dashboard. Because this is ordinary terminal output, terminal-native mouse-wheel scrollback and text search remain available. Press `Esc` to clear the log view and return to the dashboard without detaching; `Ctrl+C` keeps its normal command cancellation behavior.

Quitting or pressing `Ctrl+C` from `attach` only disconnects the dashboard; it does not stop applications. Use `S`, `Ctrl+S`, or `abpdev stop` for explicit process control.

## Context and Reconciliation Rules

- A context is the canonical working directory plus its resolved root YAML path.
- Project selectors are applied before the immutable launch plan is sent to the runner.
- Starting the same application again does not create a duplicate process.
- Active applications keep their original launch plan when YAML or CLI options change. Stop and run them again to apply changes.
- A scoped stop leaves other directories and YAML contexts untouched. `stop --all` is the only stop mode that deliberately crosses context boundaries.

Runner communication uses a current-user-only named pipe and a per-user authentication token. Captured logs are bounded in memory and persisted with size-based rotation.
