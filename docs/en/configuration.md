---
id: configuration
title: abpdev.yml Configuration
---

# `abpdev.yml` Configuration

`abpdev.yml` is the project-local configuration file used by `abpdev run`. It can define solution-wide project selection and migration behavior, as well as launch settings and environment variables for individual .NET and npm applications.

The file has two top-level sections:

- `run` configures application discovery, selection, and launch behavior.
- `environment` applies a named virtual environment and custom environment variables to launched processes.

`abpdev prepare` can generate an `abpdev.yml` containing the detected environment. You can then add any of the settings documented below.

## Complete Example

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
    - angular
  msbuild-properties:
    UseMudBlazor: "true"
    DefineConstants: "FEATURE_A;FEATURE_B"
  npm:
    scripts:
      - start:dev
      - dev

environment:
  name: SqlServer
  variables:
    ASPNETCORE_ENVIRONMENT: Development
    ConnectionStrings__Default: "Server=localhost;Database={AppName}_{Today};Trusted_Connection=True;TrustServerCertificate=True"
```

All property names use hyphen-case. Environment variable names and MSBuild property names keep their original casing.

## File Lookup

For `abpdev run <working-directory>`, AbpDevTools loads `<working-directory>/abpdev.yml` as the root configuration. It does not look for `.abpdev.yml` or a user-level `abpdev.yml`.

When each application is launched, AbpDevTools searches for the nearest `abpdev.yml`, starting in the application's directory and continuing through its parent directories. The first file found is used for that application. Files are not merged, so a nearer project file replaces the root file for project launch and environment settings.

Use a project-level file when one application needs different launch settings:

```text
MySolution/
|-- abpdev.yml
|-- services/
|   `-- orders/
|       |-- OrdersService.csproj
|       `-- abpdev.yml
```

The root file can select all applications:

```yaml
run:
  projects:
    - MyApp.HttpApi.Host
    - OrdersService
```

The nearer `services/orders/abpdev.yml` can configure only the orders service:

```yaml
run:
  configuration: Release
  msbuild-properties:
    EnablePreviewFeatures: "true"

environment:
  name: PostgreSql
```

Because files are not merged, repeat any root launch or environment settings that the project-level file should retain.

### Alternate Root File

The `--yml` option loads an alternate root configuration:

```bash
abpdev run --yml profiles/development.yml
```

The alternate file controls root-scoped settings such as `projects`, `skip-migrate`, `skip-check-libs`, and `npm.scripts`. Application launch settings still use the nearest file named `abpdev.yml`.

## `run` Section

| Property | Type | Default | Scope and behavior |
|----------|------|---------|--------------------|
| `watch` | Boolean | `false` | Uses `dotnet watch` for .NET applications. Read from the nearest application configuration when it has a `run` section. |
| `no-build` | Boolean | `false` | Passes `--no-build` to `dotnet run`. Read from the nearest application configuration. |
| `graph-build` | Boolean | `false` | Passes `/graphBuild` to `dotnet run`. Read from the nearest application configuration. |
| `configuration` | String | Not set | Passes `--configuration <value>` to `dotnet run`. Read from the nearest application configuration. |
| `skip-migrate` | Boolean | `false` | Skips the migration step before applications start. Read from the root configuration. |
| `skip-check-libs` | Boolean | `false` | Skips the `wwwroot/libs` check and installation prompt. Read from the root configuration. |
| `projects` | String array | Empty | Selects applications using case-insensitive name or path fragments. Read from the root configuration. |
| `msbuild-properties` | Mapping | Empty | Passes each entry as `--property:Name=Value` to `dotnet run`. Read from the nearest application configuration. |
| `npm.scripts` | String array | Empty | Lists npm script names in priority order. Read from the root configuration. |

### Project Selection

`run.projects` has the same matching behavior as the `--projects` command-line option. Each value is a case-insensitive fragment matched against the application name, path, or npm display name.

```yaml
run:
  projects:
    - HttpApi.Host
    - angular
```

When `--projects` is supplied on the command line, it replaces `run.projects`.

### MSBuild Properties

`run.msbuild-properties` is a mapping of MSBuild property names to values:

```yaml
run:
  msbuild-properties:
    UseMudBlazor: "true"
    DefineConstants: "FEATURE_A;FEATURE_B"
    EmptyValue:
```

Quote values that contain spaces, semicolons, or YAML boolean-like values. A null value is passed as an empty value.

Command-line `--msbuild-property Name=Value` entries are merged with the file. A command-line entry replaces a file entry with the same name, using a case-insensitive comparison.

### npm Scripts

For each `package.json`, AbpDevTools uses the first configured script that exists:

```yaml
run:
  npm:
    scripts:
      - start:dev
      - dev
      - serve
```

If none of the configured scripts exist, discovery falls back to `dev`, then `serve`, then `start`. A configured script is considered safe for automatic execution, including with `abpdev run --all`.

## `environment` Section

| Property | Type | Default | Behavior |
|----------|------|---------|----------|
| `name` | String | Not set | Applies a virtual environment configured by `abpdev env config`. |
| `variables` | String mapping | Empty | Sets or replaces environment variables for the launched process. |

```yaml
environment:
  name: SqlServer
  variables:
    ASPNETCORE_ENVIRONMENT: Development
    Redis__Configuration: localhost:6379
```

The named virtual environment is applied first. Values in `environment.variables` are applied afterward and replace variables with the same name.

The variables are applied only to processes started by `abpdev run`; they do not modify the parent shell or machine environment.

### Value Placeholders

Environment variable values support these placeholders:

| Placeholder | Replacement |
|-------------|-------------|
| `{Today}` | Current local date in `yyyyMMdd` format. |
| `{AppName}` | A normalized application name derived from the target application's working directory. |

## Command-Line Precedence

| Setting | Effective value |
|---------|-----------------|
| `watch` | Nearest application configuration when it has a `run` section; otherwise command-line `--watch`. |
| `no-build`, `graph-build` | Enabled when either the command-line option or nearest application configuration is `true`. |
| `skip-migrate`, `skip-check-libs` | Enabled when either the command-line option or root configuration is `true`. |
| `projects` | Command-line `--projects` when supplied; otherwise root `run.projects`. |
| `configuration` | Nearest application configuration when set; otherwise command-line `--configuration`. |
| `msbuild-properties` | File and command-line mappings are merged; command-line values replace duplicate names. |
| `environment` | The nearest file's named environment, then its variables, then command-line `--env`; later values replace duplicate variables. |

The `no-build`, `graph-build`, `skip-migrate`, and `skip-check-libs` options are additive: there is no command-line option that changes a `true` file value back to `false`.

## Related Configuration

`abpdev.yml` is separate from the global configuration files managed by other commands:

- [Virtual Environments](environment/virtual-environments.md) defines reusable named environment variable sets.
- [Environment Apps](environment/environment-apps.md) configures infrastructure applications such as database and cache containers.
- [Local Sources](references/local-sources.md) maps NuGet packages to local source repositories.
