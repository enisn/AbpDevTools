---
id: virtual-environments
title: Virtual Environments
---

# Virtual Environments

Virtual environments allow you to run multiple solutions with different configurations. This is particularly useful when you need to run different solutions with different connection strings, environment variables, or settings.

## Overview

Virtual environments solve the problem of managing different configurations for:
- Different databases (SQL Server, PostgreSQL, MongoDB)
- Different connection strings
- Different service URLs
- Different environment variables

## Configure Virtual Environments

### Using the Command

```bash
abpvdev env config
```

This opens an interactive configuration tool where you can:
- Add new environments
- Edit existing environments
- Delete environments
- Select active environment

## Configuration File

The virtual environments are stored in your `abpvdev.yml` file:

```json
{
  "Environments": {
    "SqlServer": {
      "Variables": {
        "ConnectionStrings__Default": "Server=localhost;Database={AppName}_{Today};User ID=SA;Password=12345678Aa;TrustServerCertificate=True"
      }
    },
    "MongoDB": {
      "Variables": {
        "ConnectionStrings__Default": "mongodb://localhost:27017/{AppName}_{Today}"
      }
    }
  }
}
```

## Placeholders

| Placeholder | Description | Example |
|-------------|-------------|---------|
| `{AppName}` | Application name | MyApp |
| `{Today}` | Current date (yyyy-MM-dd) | 2024-01-15 |

### Example

With `{AppName}_{Today}`:
- SQL Server: `Server=localhost;Database=MyApp_2024-01-15;...`
- MongoDB: `mongodb://localhost:27017/MyApp_2024-01-15`

This allows running multiple instances with separate databases each day.

## Using Virtual Environments

### Run with Environment

```bash
abpvdev run -e SqlServer
```

Uses the SqlServer environment configuration.

### Build with Environment

```bash
abpvdev build -e SqlServer
```

### Add Custom Variables

```json
{
  "Environments": {
    "Development": {
      "Variables": {
        "ConnectionStrings__Default": "Server=localhost;Database=MyApp_Dev",
        "App__SelfUrl": "https://localhost:44350",
        "AuthServer__Authority": "https://localhost:44350"
      }
    }
  }
}
```

## Common Use Cases

### Multiple Database Providers

Run the same solution with different databases:

```bash
# Run with SQL Server
abpvdev run -e SqlServer

# Run with MongoDB
abpvdev run -e MongoDB
```

### Development vs Production

```json
{
  "Environments": {
    "Development": {
      "Variables": {
        "ConnectionStrings__Default": "Server=localhost;Database=MyApp_Dev"
      }
    },
    "Production": {
      "Variables": {
        "ConnectionStrings__Default": "Server=prodserver;Database=MyApp_Prod"
      }
    }
  }
}
```

## Troubleshooting

### Environment Not Found

Make sure the environment is defined in your `abpvdev.yml` file.

### Variables Not Applied

Check that the variable names match your application's configuration keys exactly.

### Placeholder Not Replaced

Ensure placeholders are in curly braces: `{AppName}` not `AppName`.

## Next Steps

- [Environment Apps](environment-apps.md) - Manage database servers
- [Configuration](../configuration.md) - Full configuration guide
