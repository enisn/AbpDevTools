---
id: getting-started
title: Getting Started
---

# Getting Started

This guide will help you get started with AbpDevTools and set up your first ABP project.

## Video Tutorial

Watch the Getting Started video for a visual walkthrough:

[![Getting Started Video](https://github.com/enisn/AbpDevTools/assets/23705418/b31a37a0-96c7-418c-8287-80922c178b3c)](https://youtu.be/wG7MfdIq_Fo)

## Quick Start

### 1. Verify Installation

First, verify that AbpDevTools is installed correctly:

```bash
abpvdev --help
```

### 2. Navigate to Your Project

Navigate to your ABP solution directory:

```bash
cd path/to/your/abp/solution
```

### 3. Prepare Your Project

Run the prepare command to set up your project for development:

```bash
abpvdev prepare
```

This will:
- Detect project dependencies (SQL Server, MongoDB, Redis, etc.)
- Start required environment apps (databases, message brokers)
- Install ABP libraries
- Create local configuration files

### 4. Run Your Project

Start your application:

```bash
abpvdev run
```

This will:
- Automatically detect your applications
- Prompt you to select which projects to run
- Build and start all selected projects

## Understanding Project Detection

AbpDevTools automatically detects different types of projects in your solution:

| Project Type | Detection Criteria | Example |
|--------------|-------------------|---------|
| Web Application | Contains "HttpApi.Host" or "Web" in name | `MyApp.Web.HttpApi.Host` |
| Blazor WASM | Contains "Blazor" in name | `MyApp.Blazor.Web` |
| Mobile App | Contains "Mobile" or "Maui" in name | `MyApp.Mobile.iOS` |
| DbMigrator | Contains "DbMigrator" in name | `MyApp.DbMigrator` |

## Common Workflows

### Running Multiple Solutions

If you have multiple solutions in your directory:

```bash
abpvdev run
```

AbpDevTools will detect all solutions and prompt you to select which one(s) to run.

### Using Virtual Environments

Create a virtual environment for different database configurations:

```bash
abpvdev env config
```

Then use it when running your project:

```bash
abpvdev run -e SqlServer
```

This will use the SqlServer environment configuration.

### Building Multiple Projects

Build all projects in your solution:

```bash
abpvdev build
```

Or build specific projects:

```bash
abpvdev build -f MyApp.Web.MyApp
```

## Configuration Files

AbpDevTools uses YAML configuration files:

### abpvdev.yml

Located in your project directory, this file configures:
- Project naming conventions
- Default build options
- Environment variables
- Custom scripts

### .abpvdev-local.yml

Local configuration that won't be committed to version control. Used for machine-specific settings.

## Next Steps

- Explore [Commands](commands/build.md) for detailed command documentation
- Learn about [Virtual Environments](environment/virtual-environments.md)
- Set up [Reference Management](references/local-sources.md) for local development
