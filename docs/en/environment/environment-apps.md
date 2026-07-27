---
id: environment-apps
title: Environment Apps
---

# Environment Apps

Environment apps allow you to easily run commonly used infrastructure services like databases and message brokers. AbpDevTools can start, stop, and manage these services using Docker.

## Available Environment Apps

### Default Apps

| App Name | Default Credentials | Default Port |
|----------|---------------------|--------------|
| `sqlserver` | `sa` / `yourStrong(!)Password` | 1433 |
| `sqlserver-edge` | `sa` / `yourStrong(!)Password` | 1433 |
| `postgresql` | `postgres` / `postgres` | 5432 |
| `mysql` | `root` / `root` | 3306 |
| `mongodb` | No authentication | 27017 |
| `redis` | No authentication | 6379 |
| `rabbitmq` | `guest` / `guest` | 5672 |

These credentials are intended for local development only. Do not expose
containers using these defaults to untrusted networks.

## Commands

### Start an Environment App

```bash
abpdev envapp start <appname> [options]
```

### Stop an Environment App

```bash
abpdev envapp stop <appname> [options]
```

## Options

| Option | Shortcut | Description |
|--------|----------|-------------|
| `--password` | `-p` | Override the configured database password |
| `--verbose` | `-v` | Show detailed Docker command output |
| `--help` | `-h` | Shows help text |

## Examples

### Start SQL Server

```bash
abpdev envapp start sqlserver
```

### Start SQL Server with Custom Password

```bash
abpdev envapp start sqlserver -p myPassw0rd
```

The password override applies when the container is created. If the named
container already exists, Docker starts it with its existing credentials.
Update the matching virtual-environment connection string when using a custom
password.

### Start PostgreSQL

```bash
abpdev envapp start postgresql
```

### Start MongoDB

```bash
abpdev envapp start mongodb
```

### Start Redis

```bash
abpdev envapp start redis
```

### Start RabbitMQ

```bash
abpdev envapp start rabbitmq
```

### Stop an App

```bash
abpdev envapp stop sqlserver
```

## Configuration

### Customizing Default Commands

You can customize the Docker commands used to start each app:

```bash
abpdev envapp config
```

This opens `%AppData%/abpdev/environment-tools.yml` where you can:
- Add new environment apps
- Modify existing app configurations
- Change the default password used when `-p` is omitted
- Change Docker commands and images

### Example Custom Configuration

```yaml
custom-postgres:
  DefaultPassword: mypassword
  StartCmds:
    - docker start custom-postgres
    - docker run --name custom-postgres --restart unless-stopped -e "POSTGRES_PASSWORD=Passw0rd" -p 5433:5432 -d postgres:15
  StopCmds:
    - docker kill custom-postgres
    - docker rm custom-postgres
```

## Prerequisites

- Docker Desktop must be installed and running
- Sufficient system resources (RAM, disk space)
- Appropriate permissions to run Docker commands

## Troubleshooting

### Docker Not Running

Make sure Docker Desktop is installed and running. Check with:

```bash
docker ps
```

### Port Already in Use

Run `abpdev envapp config` and change the host-side port in the configured
Docker command.

### Permission Denied

On Linux, you may need to run Docker with sudo or add your user to the docker group.

### Container Not Starting

Check Docker logs:

```bash
docker logs <container-name>
```

## Automatic Starting

Environment apps can be automatically started when using `abpdev prepare`:

The prepare command detects your project's dependencies and automatically starts the required environment apps.

## Connection Strings

### SQL Server

```
Server=localhost;Database={AppName};User ID=SA;Password=yourStrong(!)Password;TrustServerCertificate=True
```

### PostgreSQL

```
Server=localhost;Port=5432;Database={AppName};User Id=postgres;Password=postgres;
```

### MySQL

```
Server=localhost;Port=3306;Database={AppName};User Id=root;Password=root;
```

### MongoDB

```
mongodb://localhost:27017/{AppName}
```

### Redis

```
localhost:6379
```

## Next Steps

- [Virtual Environments](virtual-environments.md) - Configure different environments
- [Configuration](../configuration.md) - Full configuration guide
