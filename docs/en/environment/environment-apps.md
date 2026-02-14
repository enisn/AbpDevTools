---
id: environment-apps
title: Environment Apps
---

# Environment Apps

Environment apps allow you to easily run commonly used infrastructure services like databases and message brokers. AbpDevTools can start, stop, and manage these services using Docker.

## Available Environment Apps

### Default Apps

| App Name | Description | Default Port |
|----------|-------------|---------------|
| `sqlserver` | SQL Server | 1433 |
| `sqlserver-edge` | SQL Server Edge | 1433 |
| `postgresql` | PostgreSQL | 5432 |
| `mysql` | MySQL | 3306 |
| `mongodb` | MongoDB | 27017 |
| `redis` | Redis | 6379 |
| `rabbitmq` | RabbitMQ | 5672 |

## Commands

### Start an Environment App

```bash
abpvdev envapp start <appname> [options]
```

### Stop an Environment App

```bash
abpvdev envapp stop <appname> [options]
```

## Options

| Option | Shortcut | Description |
|--------|----------|-------------|
| `--password` | `-p` | Custom password (for SQL Server, MySQL) |
| `--port` | | Custom port |
| `--help` | `-h` | Shows help text |

## Examples

### Start SQL Server

```bash
abpvdev envapp start sqlserver
```

### Start SQL Server with Custom Password

```bash
abpvdev envapp start sqlserver -p myPassw0rd
```

### Start PostgreSQL

```bash
abpvdev envapp start postgresql
```

### Start MongoDB

```bash
abpvdev envapp start mongodb
```

### Start Redis

```bash
abpvdev envapp start redis
```

### Start RabbitMQ

```bash
abpvdev envapp start rabbitmq
```

### Start on Custom Port

```bash
abpvdev envapp start sqlserver --port 1434
```

### Stop an App

```bash
abpvdev envapp stop sqlserver
```

## Configuration

### Customizing Default Commands

You can customize the Docker commands used to start each app:

```bash
abpvdev envapp config
```

This opens the configuration file where you can:
- Add new environment apps
- Modify existing app configurations
- Change Docker commands and images

### Example Custom Configuration

```json
{
  "EnvironmentApps": {
    "custom-postgres": {
      "Image": "postgres:15",
      "Ports": {
        "5432": "5433"
      },
      "Environment": {
        "POSTGRES_PASSWORD": "mypassword"
      }
    }
  }
}
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

Specify a different port:

```bash
abpvdev envapp start sqlserver --port 1434
```

### Permission Denied

On Linux, you may need to run Docker with sudo or add your user to the docker group.

### Container Not Starting

Check Docker logs:

```bash
docker logs <container-name>
```

## Automatic Starting

Environment apps can be automatically started when using `abpvdev prepare`:

The prepare command detects your project's dependencies and automatically starts the required environment apps.

## Connection Strings

### SQL Server

```
Server=localhost;Database={AppName};User ID=SA;Password=yourpassword;TrustServerCertificate=True
```

### PostgreSQL

```
Host=localhost;Database={AppName};Username=postgres;Password=yourpassword
```

### MySQL

```
Server=localhost;Database={AppName};User ID=root;Password=yourpassword
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
