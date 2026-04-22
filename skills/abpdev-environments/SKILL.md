---
name: abpdev-environments
description: >-
  Manage AbpDevTools virtual environments and environment apps with abpdev. Use
  when the user wants to configure connection-string environments, start/stop
  Docker-backed infra tools, or open a shell with a selected environment.
---

# abpdev environments

Use this skill for `env`, `envapp`, and process environment switching.

## Covered commands

| Command | Purpose |
|---|---|
| `abpdev env` | Explain virtual environment usage |
| `abpdev env config` | Open environment configuration |
| `abpdev envapp` | Show environment app help/context |
| `abpdev envapp config` | Open environment-app command configuration |
| `abpdev envapp start` | Start infra tools such as SQL Server, Redis, RabbitMQ |
| `abpdev envapp stop` | Stop one or all configured infra tools |
| `abpdev switch-to-env` | Open a terminal with a selected environment applied |
| `abpdev run --env <name>` | Run apps with a virtual environment |

## Config file locations

Global configs are under:

```text
%AppData%\abpdev
```

Main files in this area:

- `EnvironmentConfiguration.yml` via `abpdev env config`
- `environment-tools.yml` via `abpdev envapp config`
- `tools-configuration.yml` for terminal/open command overrides

## Virtual environments

`abpdev env config` manages named sets of environment variables, mainly for connection strings.

Default environment names include:

- `SqlServer`
- `MongoDb`
- `PostgreSql`
- `MySql`

Example shape:

```yaml
SqlServer:
  variables:
    ConnectionStrings__Default: Server=localhost;Database={AppName}_{Today};User ID=SA;Password=12345678Aa;TrustServerCertificate=True

MongoDb:
  variables:
    ConnectionStrings__Default: mongodb://localhost:27017/{AppName}_{Today}
```

Tokens like `{AppName}` and `{Today}` are intended for templated environment values.

## Using an environment

Apply it directly while running:

```bash
abpdev run --env SqlServer
abpdev run --env PostgreSql -p MyApp.HttpApi.Host
```

Or open a new terminal with that environment applied:

```bash
abpdev switch-to-env SqlServer
```

On Windows this uses the configured terminal command, which defaults to `wt`.

## Environment apps

`abpdev envapp start` and `abpdev envapp stop` manage external infrastructure, usually through Docker commands stored in config.

Default app keys include:

- `sqlserver`
- `sqlserver-edge`
- `postgresql`
- `mysql`
- `mongodb`
- `redis`
- `rabbitmq`

Examples:

```bash
abpdev envapp start sqlserver
abpdev envapp start redis rabbitmq
abpdev envapp start sqlserver -p MyStrongPassword123! -v
abpdev envapp stop sqlserver
abpdev envapp stop all
```

Notes:

- `envapp start` can take multiple app names.
- `-p`, `--password` replaces the placeholder `Passw0rd` inside configured start commands.
- `-v`, `--verbose` shows Docker command output.
- `envapp stop` currently expects Docker-based stop commands.

## Configuration format

`abpdev envapp config` opens a YAML dictionary keyed by app name.

Preferred schema:

```yaml
sqlserver:
  start-cmds:
    - docker start tmp-sqlserver
    - docker run --name tmp-sqlserver --restart unless-stopped -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=Passw0rd" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2017-CU8-ubuntu
  stop-cmds:
    - docker kill tmp-sqlserver
    - docker rm tmp-sqlserver
```

Legacy single-string keys are still supported, but arrays are preferred.

## Guidance for agents

- Use `abpdev run --env <name>` when the user wants an environment only for that run.
- Use `abpdev switch-to-env <name>` when the user explicitly wants a shell/session switched.
- Use `abpdev envapp start` only for the infra tools the project actually needs.
- If the user is onboarding a project, `abpdev prepare` may be a better entry point because it can infer and start required env apps automatically.

## Troubleshooting

**App name is not recognized**
- Run `abpdev envapp config` and verify the key name.

**Wrong terminal opens for `switch-to-env`**
- Check `abpdev tools config` and update the `terminal` tool path.

**Docker command needs customization**
- Edit `environment-tools.yml` via `abpdev envapp config`.
