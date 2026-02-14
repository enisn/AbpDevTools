---
id: database-commands
title: Database Commands
---

# Database Commands

AbpDevTools provides commands for database management operations.

## Commands Overview

| Command | Description |
|---------|-------------|
| `abpvdev migrate` | Runs database migrations |
| `abpvdev database-drop` | Drops the database |

## Migrate Command

Runs pending Entity Framework Core migrations to update the database schema.

### Usage

```
abpvdev migrate [workingdirectory] [options]
```

### Options

| Option | Shortcut | Description |
|--------|----------|-------------|
| `--project` | `-p` | Path to the DbMigrator project |
| `--connection-string` | `-c` | Override connection string |
| `--verbose` | `-v` | Show detailed output |
| `--help` | `-h` | Shows help text |

### Examples

```bash
abpvdev migrate
abpvdev migrate -p MyProject.DbMigrator
abpvdev migrate -c "Server=localhost;Database=MyDb"
```

### How It Works

1. Locates the DbMigrator project
2. Builds the project
3. Applies pending migrations
4. Seeds the database if needed

## Drop Database Command

Drops the database for your application.

### Usage

```
abpvdev database-drop [options]
```

### Options

| Option | Shortcut | Description |
|--------|----------|-------------|
| `--project` | `-p` | Path to the DbMigrator project |
| `--connection-string` | `-c` | Database connection string |
| `--force` | `-f` | Drop without confirmation |
| `--help` | `-h` | Shows help text |

### Examples

```bash
abpvdev database-drop
abpvdev database-drop -f
abpvdev database-drop -p MyProject.DbMigrator
```

### Warning

This command permanently deletes your database. Use with caution!

## Workflow

Typical database workflow:

1. **Prepare project**: `abpvdev prepare` (starts databases)
2. **Run migrations**: `abpvdev migrate`
3. **Start application**: `abpvdev run`
4. **Drop and recreate**: 
   ```bash
   abpvdev database-drop -f
   abpvdev migrate
   ```

## Database Providers

AbpDevTools supports various database providers:
- SQL Server
- PostgreSQL
- MySQL
- MongoDB
- SQLite

## Troubleshooting

### Connection Failed

Ensure the database server is running:
```bash
abpvdev envapp start sqlserver
```

### Migration Failed

Check the database server is accessible and connection strings are correct in your configuration.

### Permission Denied

Make sure your database user has sufficient permissions to create/modify the database.
