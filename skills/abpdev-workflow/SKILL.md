---
name: abpdev-workflow
description: >-
  Run and manage the core AbpDevTools developer workflow: build, migrate,
  launch, inspect, log, stop, test, prepare, and bundle ABP applications. Use
  for foreground or agent-managed background application workflows.
---

# abpdev workflow

Use this skill for the day-to-day application workflow commands.

## Covered commands

| Command | Purpose |
|---|---|
| `abpdev build` | Recursively build solutions/projects |
| `abpdev migrate` | Run `.DbMigrator` projects or fallback `--migrate-database` apps |
| `abpdev run` | Run app projects through the centralized runner and optionally run migrators |
| `abpdev ps` | List applications managed across active runner contexts |
| `abpdev stop` | Stop selected applications, one context, or every active runner context |
| `abpdev attach` | Open the interactive state-and-log dashboard for a context |
| `abpdev test` | Recursively run `dotnet test` |
| `abpdev prepare` | Prepare the project on a new machine |
| `abpdev logs` | Return captured process logs, with filesystem fallback |
| `abpdev bundle` | Run `abp bundle` for Blazor WASM projects |
| `abpdev bundle list` | List Blazor WASM projects that need bundling |

## Prerequisites

```bash
dotnet tool update -g AbpDevTools
```

## build

```bash
abpdev build [working-directory] [options]
```

Useful options:

- `-f`, `--build-files`: filter target `.sln`, `.slnx`, or `.csproj` files by name
- `-i`, `--interactive`: choose targets interactively
- `--dry-run`: list the selected targets and separate solution/project counts without building
- `-c`, `--configuration`: pass build configuration

Behavior:

- Searches recursively for `.sln` and `.slnx`, with filters and interactive selection determining the final targets
- Falls back to `.csproj` only when no solutions are selected
- A dry run lists the exact selected solution/project targets and reports their counts separately without running `dotnet build` or sending a completion notification
- A normal build uses `dotnet build /graphBuild`

```bash
abpdev build --dry-run
```

## run

```bash
abpdev run [working-directory] [options]
```

Useful options:

- `-a`, `--all`: run all discovered app projects
- `-p`, `--projects`: filter projects by name/path fragment
- `-w`, `--watch`: run in watch mode
- `--skip-migrate`: skip `.DbMigrator` projects
- `--no-build`: pass `--no-build` to `dotnet run`
- `-g`, `--graphBuild`: use graph build behavior
- `-i`, `--install-libs`: run `abp install-libs`
- `--skip-check-libs`: skip missing `wwwroot/libs` checks
- `-e`, `--env`: apply a configured virtual environment
- `-r`, `--retry`: retry when apps exit
- `-v`, `--verbose`: show verbose project output
- `--yml`: explicitly point to an exact `abpdev.yml` path
- `-d`, `--detach`: launch through the centralized runner and return immediately

Behavior:

- Loads the nearest `abpdev.yml` from the working directory or its parents
- Runs migrators first unless skipped
- Prompts for project selection when multiple runnable apps are found and interactive input is available
- Can detect missing `wwwroot/libs` and offer to run `abp install-libs`
- Always delegates application ownership and output capture to the per-user centralized runner
- Opens the interactive dashboard by default; `--detach` leaves applications running and returns control to the caller

## Centralized runner lifecycle

```bash
abpdev ps
abpdev ps --current --json
abpdev stop [working-directory] -p MyApp.HttpApi.Host
abpdev stop --all --force
abpdev attach [working-directory]
```

Behavior:

- The runner starts on demand and exits shortly after its last application stops.
- A context is the canonical working directory plus the resolved root `abpdev.yml` path.
- `abpdev ps` lists active applications from every context by default. Use `--current` or a positional directory to scope it, `--all` to include inactive entries, and `--json` for machine-readable state when the runner is available. If no runner exists, the command returns a plain explanatory message.
- With no directory, `abpdev stop` uses the exact current context or a unique nearest active ancestor. If neither matches, it offers an interactive context picker; non-interactive callers receive help and copyable explicit context commands.
- An explicit directory or `--yml` keeps `abpdev stop` scoped to that exact context. Without `-p`, it stops that context's active applications; repeated `-p`/`--projects` selectors restrict the operation.
- `abpdev stop --all` stops active applications across every context after an interactive confirmation. Non-interactive callers must use `abpdev stop --all --force`; `--all` cannot be combined with a directory, `--yml`, or project selectors.
- If `run` used an explicit `--yml`, pass that same path to scoped `ps`, `stop`, or `attach` commands so they resolve the same context identity.
- Starting an already active application does not create a duplicate process.
- `abpdev attach` opens the interactive dashboard. Leaving an attached dashboard does not stop its applications; use `abpdev stop` for explicit lifecycle control.

## migrate

```bash
abpdev migrate [working-directory] [options]
```

Useful options:

- `--no-build`: pass `--no-build` to migrator runs
- `-e`, `--env`: apply a configured virtual environment
- `-a`, `--all`: run all matching migrators/fallback projects
- `-p`, `--projects`: filter projects by name/path fragment

Behavior:

- Finds `.DbMigrator` executable projects recursively and runs them
- If none are found, looks for runnable projects supporting `--migrate-database`
- Applies local `abpdev.yml` environment settings and optional `--env` overrides

## Local YAML

Project-local run settings live in `abpdev.yml` and can include:

```yaml
run:
  watch: false
  no-build: false
  graph-build: false
  configuration: Debug
  skip-migrate: false
  skip-check-libs: false
  projects:
    - MyApp.HttpApi.Host
  msbuild-properties:
    UseMudBlazor: true

environment:
  name: SqlServer
```

## test

```bash
abpdev test [working-directory] [options]
```

Useful options:

- `-f`, `--files`: filter solutions by name/path fragment
- `-i`, `--interactive`: select target solutions interactively
- `-c`, `--configuration`: pass test configuration
- `--no-build`: pass `--no-build` to `dotnet test`

Behavior:

- Searches `.sln` and `.slnx`
- Does not fall back to `.csproj` test discovery today

## prepare

```bash
abpdev prepare [working-directory] [options]
```

Useful options:

- `--no-config`: do not create `abpdev.yml`
- `--no-install-libs`: skip `abp install-libs`
- `--no-env-apps`: skip environment app startup
- `--no-bundle`: skip Blazor WASM bundling

Behavior:

- Scans runnable projects for infrastructure dependencies
- Starts needed environment apps unless disabled
- Creates local `abpdev.yml` files when it can infer an environment
- Runs `abp install-libs`
- Bundles Blazor WASM projects unless disabled

## logs

```bash
abpdev logs <project-name> -p <working-directory>
abpdev logs -i -p <working-directory>
abpdev logs <project-name> --lines 20
abpdev logs <project-name> --follow
abpdev logs --managed --lines 200
abpdev logs <project-name> --open
```

Behavior:

- For a named project with an active managed process, returns captured stdout/stderr.
- If no matching process is active, explicitly reports the fallback and reads `<project>/Logs/logs.txt`.
- Prints at most the last 100 lines by default and exits immediately.
- `-n`, `--lines` controls the maximum returned lines for managed and filesystem logs.
- `-f`, `--follow` follows managed output until cancellation; avoid it for bounded agent checks.
- `--managed` without a project combines captured logs for the current context.
- `-o`, `--open` opens the log file or folder with the OS default app instead

## bundle

```bash
abpdev bundle [working-directory] [-g]
abpdev bundle list [working-directory]
```

Behavior:

- Detects Blazor WebAssembly projects by SDK name
- Runs `abp bundle -wd <project-dir>` for each detected WASM project
- `-g`, `--graphBuild` builds the project first

## Guidance for agents

- Use `abpdev prepare` for onboarding/new-machine setup, not as a default replacement for `run`.
- Use `abpdev run --yml <path>` when the repo has multiple startup contexts.
- Use `abpdev bundle list` before `abpdev bundle` if the user only wants discovery.
- Prefer explicit `--projects` filters in large monorepos to avoid launching unrelated apps.
- For agent work, use `abpdev run --detach` so the launch command returns, then poll `abpdev ps --current --json` for state/readiness. A successful launch request does not by itself mean every application is ready.
- Use `abpdev logs <project> --lines <count>` for bounded diagnostics. It is non-interactive and automatically chooses active runner output or filesystem fallback.
- Use `abpdev stop -p <project>` for a selected app or unfiltered `abpdev stop` only when the user intends to stop the entire current context.
- Use `abpdev stop --all --force` only when the user explicitly intends to stop every managed application across every active context. Agents and CI must include `--force`; omitting it deliberately fails with corrective usage instead of stopping anything.
- Do not use `abpdev attach` in non-interactive automation; it is the human dashboard and fails fast with command help when no interactive terminal is available. Use `abpdev ps --json` for state and bounded `abpdev logs` calls for diagnostics.

## Typical workflows

```bash
# First-time setup on a machine
abpdev prepare

# Human-attached daily development run
abpdev run -e SqlServer

# Agent-managed run, readiness check, bounded logs, and scoped stop
abpdev run --detach --skip-migrate -p MyApp.HttpApi.Host
abpdev ps --current --json
abpdev logs MyApp.HttpApi.Host --lines 200
abpdev stop -p MyApp.HttpApi.Host

# Run only database migrations
abpdev migrate -e SqlServer

# Run a filtered set of apps without migration
abpdev run --skip-migrate -p AuthServer HttpApi.Host

# Build and test only selected solutions
abpdev build -f MyApp
abpdev test -f MyApp --no-build
```
