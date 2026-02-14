---
id: configuration
title: Configuration
---

# Configuration

AbpDevTools uses YAML configuration files to customize its behavior. This guide covers all configuration options.

## Configuration Files

### Main Configuration File

The primary configuration file is `abpvdev.yml` in your project directory.

### Configuration Locations

AbpDevTools looks for configuration in this order:
1. `.abpvdev.yml` (local, not committed to version control)
2. `abpvdev.yml` (project-level)
3. `~/.abpvdev.yml` (user-level global config)

Settings from later files override earlier ones.

## Configuration Sections

### Run Configuration

Customize how projects are run:

```yaml
run:
  # Application project naming patterns
  application:
    - "*Web.Host"
    - "*HttpApi.Host"
    - "*Web"
  
  # DbMigrator pattern
  dbmigrator:
    - "*DbMigrator"
  
  # Blazor WASM pattern
  blazor:
    - "*Blazor.Web"
    - "*Blazor.Client"
```

### Build Configuration

```yaml
build:
  # Build configuration
  configuration: Debug
  
  # Project patterns to include
  projects:
    - "*.sln"
    - "*.csproj"
  
  # Projects to exclude
  exclude:
    - "*.Tests.csproj"
```

### Replacement Configuration

```yaml
replacement:
  ConnectionStrings:
    FilePattern: "appsettings*.json"
    Find: "Trusted_Connection=True;"
    Replace: "User ID=SA;Password=12345678Aa;"
```

### Environment Variables

```yaml
environment:
  Variables:
    ConnectionStrings__Default: "Server=localhost;Database=MyApp"
    AuthServer__Authority: "https://localhost:44350"
```

### Virtual Environments

```yaml
Environments:
  SqlServer:
    Variables:
      ConnectionStrings__Default: "Server=localhost;Database={AppName}_{Today}..."
  MongoDB:
    Variables:
      ConnectionStrings__Default: "mongodb://localhost:27017/{AppName}"
```

### Environment Apps

```yaml
environmentApps:
  sqlserver:
    Image: "mcr.microsoft.com/mssql/server"
    Ports:
      "1433": "1433"
    Environment:
      ACCEPT_EULA: "Y"
      SA_PASSWORD: "yourpassword"
```

### Local Sources

```yaml
localSources:
  abp:
    Path: C:\github\abp
    Packages:
      - Volo.Abp.*
```

## Managing Configuration

### Edit Configuration

Use the config command:

```bash
abpvdev config
```

### Clear Configuration

```bash
abpvdev config clear
```

## Placeholders

Use placeholders in configuration values:

| Placeholder | Description |
|-------------|-------------|
| `{AppName}` | Application name (folder name if not detected) |
| `{Today}` | Current date (yyyy-MM-dd format) |

## Examples

### Development Setup

```yaml
run:
  application:
    - "*Web.Host"

Environments:
  Development:
    Variables:
      ConnectionStrings__Default: "Server=localhost;Database=MyApp_Dev"
```

### Production Setup

```yaml
run:
  application:
    - "*Web.Host"

Environments:
  Production:
    Variables:
      ConnectionStrings__Default: "Server=prodserver;Database=MyApp_Prod;User=produser;Password=secret"
```

## Configuration Commands

| Command | Description |
|---------|-------------|
| `abpvdev config` | Open configuration file |
| `abpvdev config clear` | Clear all configuration |
| `abpvdev run config` | Configure run settings |
| `abpvdev replace config` | Configure replacements |
| `abpvdev references config` | Configure local sources |
| `abpvdev env config` | Configure virtual environments |
| `abpvdev envapp config` | Configure environment apps |

## Troubleshooting

### Configuration Not Applied

1. Check file location and name (must be `abpvdev.yml`)
2. Verify YAML syntax (use a YAML validator)
3. Check for typos in section names

### Settings Override

Remember that later configuration files override earlier ones. Check all possible locations.
