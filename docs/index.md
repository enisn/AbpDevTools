---
id: home
title: AbpDevTools Documentation
layout: landing
---

# Welcome to AbpDevTools

AbpDevTools is a comprehensive CLI tool designed to make development with ABP Framework easier. It provides a set of utilities for building, running, managing, and configuring ABP projects efficiently.

## Key Features

- **Multi-Solution Support**: Build and run multiple solutions and projects with a single command
- **Smart Project Detection**: Automatically detects applications, DbMigrators, and web projects
- **Virtual Environments**: Run multiple solutions with different configurations (connection strings, etc.)
- **Environment Apps**: Easy management of SQL Server, PostgreSQL, MySQL, MongoDB, Redis, and RabbitMQ
- **Reference Management**: Switch between package references and local project references seamlessly
- **Notifications**: Get notified when build or run processes complete
- **ABP Studio Integration**: Switch between different ABP Studio versions easily

## Quick Links

- [Installation Guide](en/installation.md)
- [Getting Started](en/getting-started.md)
- [Commands Overview](en/commands/build.md)
- [Configuration](en/configuration.md)

## Why Use AbpDevTools?

ABP projects can be complex with multiple solutions, applications, and dependencies. AbpDevTools simplifies the development workflow by providing:

1. **Automated Project Discovery**: Automatically finds solutions and projects in your directory
2. **Smart Dependency Management**: Starts required services (databases, message brokers) automatically
3. **Flexible Configuration**: YAML-based configuration for different environments
4. **Time Savings**: Reduces repetitive tasks like switching between package and project references

## Supported Platforms

- Windows (x64/ARM)
- macOS (Intel/ARM)
- Linux

## Installation

Install AbpDevTools as a global .NET tool:

```bash
dotnet tool update -g AbpDevTools
```

For specific runtime versions:

```bash
dotnet tool update -g AbpDevTools --framework net8.0
```

## Next Steps

- Read the [Installation Guide](en/installation.md) for detailed setup instructions
- Check out the [Getting Started](en/getting-started.md) guide for your first project
- Explore individual [Commands](en/commands/build.md) for specific functionality
