---
id: migrations-commands
title: Migrations Commands
---

# Migrations Commands

AbpDevTools provides several commands for managing Entity Framework Core migrations.

## Commands Overview

| Command | Description |
|---------|-------------|
| `abpvdev migration add` | Adds a new migration |
| `abpvdev migration clear` | Clears all migrations |
| `abpvdev migration recreate` | Recreates migrations |

## Add Migration

Adds a new Entity Framework Core migration.

### Usage

```
abpvdev migration add <name> [options]
```

### Options

| Option | Shortcut | Description |
|--------|----------|-------------|
| `--project` | `-p` | Path to the DbContext project |
| `--db-context` | `-d` | Specific DbContext to use |
| `--output-dir` | `-o` | Output directory for migration files |
| `--namespace` | | Custom namespace for migration |
| `--help` | `-h` | Shows help text |

### Examples

```bash
abpvdev migration add CreateUsersTable
abpvdev migration add AddUserRoles -p MyProject.DbMigrator
abpvdev migration add UpdateProducts -d ProductDbContext
```

## Clear Migrations

Clears all migrations from the specified project.

### Usage

```
abpvdev migration clear [options]
```

### Options

| Option | Shortcut | Description |
|--------|----------|-------------|
| `--project` | `-p` | Path to the DbContext project |
| `--force` | `-f` | Force clear without confirmation |
| `--help` | `-h` | Shows help text |

### Examples

```bash
abpvdev migration clear
abpvdev migration clear -p MyProject.DbMigrator -f
```

### Warning

This command deletes all migration files. Make sure to back up your code before running.

## Recreate Migrations

Recreates migrations by clearing existing ones and adding a new initial migration.

### Usage

```
abpvdev migration recreate [options]
```

### Options

| Option | Shortcut | Description |
|--------|----------|-------------|
| `--project` | `-p` | Path to the DbContext project |
| `--name` | `-n` | Name for the new migration |
| `--force` | `-f` | Force recreate without confirmation |
| `--help` | `-h` | Shows help text |

### Examples

```bash
abpvdev migration recreate
abpvdev migration recreate -n InitialCreate -f
```

## Workflow

Typical migration workflow:

1. **Make model changes**: Update your entity classes
2. **Add migration**: `abpvdev migration add <Name>`
3. **Apply migration**: Run your application or use `abpvdev migrate`
4. **Repeat**: As your model evolves

## Troubleshooting

### No DbContext Found

Ensure you're in a project directory that contains a DbContext or specify the project with `-p`.

### Migration Conflicts

If you have merge conflicts in migration files, use `abpvdev migration recreate` to consolidate them.

### Missing Dependencies

Make sure Entity Framework Core tools are installed:
```bash
dotnet tool install --global dotnet-ef
```
