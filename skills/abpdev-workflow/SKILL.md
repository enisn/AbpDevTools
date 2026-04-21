---
name: abpdev-workflow
description: >-
  Run the core AbpDevTools developer workflow commands: build, run, test,
  prepare, logs, and bundle. Use when the user wants to build or run ABP
  solutions, prepare a machine, inspect logs, or handle Blazor WASM bundling.
---

# abpdev workflow

Use this skill for the day-to-day application workflow commands.

## Covered commands

| Command | Purpose |
|---|---|
| `abpdev build` | Recursively build solutions/projects |
| `abpdev migrate` | Run `.DbMigrator` projects or fallback `--migrate-database` apps |
| `abpdev run` | Run app projects and optionally migrators |
| `abpdev test` | Recursively run `dotnet test` |
| `abpdev prepare` | Prepare the project on a new machine |
| `abpdev logs` | Open the selected project's `Logs` folder or `logs.txt` |
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
- `-c`, `--configuration`: pass build configuration

Behavior:

- Searches recursively for `.sln` and `.slnx`
- Falls back to `.csproj` if no solutions exist
- Uses `dotnet build /graphBuild`

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
- `--yml`: explicitly point to an `abpdev.yml`

Behavior:

- Loads `abpdev.yml` from the working directory when present
- Runs migrators first unless skipped
- Prompts for project selection when multiple runnable apps are found and interactive input is available
- Can detect missing `wwwroot/libs` and offer to run `abp install-libs`

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
abpdev logs <project-name> -n 20
abpdev logs <project-name> --open
```

Behavior:

- Finds runnable projects
- Prints the last 100 lines from `<project>/Logs/logs.txt` by default
- `-n`, `--lines` controls how many lines are printed
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

## Typical workflows

```bash
# First-time setup on a machine
abpdev prepare

# Daily development run
abpdev run -e SqlServer

# Run only database migrations
abpdev migrate -e SqlServer

# Run a filtered set of apps without migration
abpdev run --skip-migrate -p AuthServer HttpApi.Host

# Build and test only selected solutions
abpdev build -f MyApp
abpdev test -f MyApp --no-build
```
