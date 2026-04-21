---
name: abpdev-migrations
description: >-
  Manage EF Core migrations and database drops across multiple ABP projects.
  Use when the user wants to add, clear, recreate migrations, or drop databases
  for one or many EntityFrameworkCore projects.
---

# abpdev migrations

Use this skill for EF Core migration and database-drop workflows.

## Covered commands

| Command | Purpose |
|---|---|
| `abpdev migrations add` | Add a migration to one or more EF Core projects |
| `abpdev migrations clear` | Delete `Migrations` folders |
| `abpdev migrations recreate` | Clear migrations, recreate an initial migration, optionally drop databases |
| `abpdev database-drop` | Drop databases for EF Core projects with design-time tools |

## Prerequisites

- AbpDevTools installed
- `dotnet-ef` available for migration commands
- Target projects should be discoverable EntityFrameworkCore projects

## Shared options

Migration commands share these selectors:

- `-a`, `--all`: run on all discovered EF Core projects
- `-p`, `--projects`: filter by project name/path fragment
- positional `working-directory`: root directory to search

If neither `--all` nor `--projects` is given, the commands use interactive project selection.

## Add migrations

```bash
abpdev migrations add [working-directory] -n Initial
abpdev migrations add -a -n AddAuditTables
abpdev migrations add -p MyApp.EntityFrameworkCore -n AddTenantIndexes
```

Behavior:

- Runs `dotnet-ef migrations add <name> --project <csproj>` for each selected project
- Shows live per-project progress/status

## Clear migrations

```bash
abpdev migrations clear
abpdev migrations clear -a
abpdev migrations clear -p MyApp.EntityFrameworkCore
```

Behavior:

- Deletes the `Migrations` folder next to each selected EF Core project

## Recreate migrations

```bash
abpdev migrations recreate
abpdev migrations recreate --drop-database
abpdev migrations recreate -p MyApp.EntityFrameworkCore --drop-database
```

Behavior:

1. Deletes each selected project's `Migrations` folder
2. Runs `dotnet-ef migrations add Initial --project <csproj>`
3. Optionally runs `dotnet ef database drop --force` in each selected project directory

Notes:

- The recreated migration name is currently `Initial`.
- `--drop-database` only affects the selected projects.

## Database drop

```bash
abpdev database-drop
abpdev database-drop --force
abpdev database-drop -e SqlServer
abpdev database-drop -p Volo.Abp.EntityFrameworkCore.SqlServer
```

Useful options:

- `-f`, `--force`: pass `--force` to EF database drop
- `-p`, `--package`: only keep projects with a direct reference to a specific package
- `-e`, `--env`: apply a configured virtual environment before dropping

Behavior:

- Finds EF Core projects with design-time tools
- Optionally filters them by direct package reference
- Runs `dotnet ef database drop`

## Guidance for agents

- Use `migrations add` for normal schema evolution.
- Use `migrations recreate` only when the user explicitly wants a reset-style workflow.
- Use `database-drop -e <env>` when connection strings should come from a named environment.
- In multi-provider repos, `database-drop --package <provider-package>` is useful to isolate SQL Server vs PostgreSQL projects.

## Troubleshooting

**No EF Core projects found**
- Confirm the working directory is the solution root.
- Confirm the EF Core project has design-time tooling where required.

**Migration creation fails**
- Ensure `dotnet-ef` is installed and available on `PATH`.
- Build the solution first if the project has compile errors.

**Wrong project was selected**
- Re-run with `-p <name-fragment>` or `--all`.
