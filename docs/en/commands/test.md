---
id: test-command
title: Test Command
---

# Test Command

The `abpvdev test` command runs tests in your ABP solution.

## Usage

```
abpvdev test [workingdirectory] [options]
```

## Parameters

| Parameter | Description |
|-----------|-------------|
| `workingdirectory` | Working directory. Default: `.` |

## Options

| Option | Shortcut | Description |
|--------|----------|-------------|
| `--filter` | `-f` | Filter tests by name/pattern |
| `--configuration` | `-c` | Build configuration (Debug/Release) |
| `--no-build` | | Skip building before testing |
| `--verbosity` | `-v` | Output verbosity (minimal, normal, detailed, diagnostic) |
| `--help` | `-h` | Shows help text |

## Examples

### Run All Tests

```bash
abpvdev test
```

### Run Tests in Specific Directory

```bash
abpvdev test C:\Path\To\Tests
```

### Filter Tests

```bash
abpvdev test --filter "FullyQualifiedName~UserService"
```

### Run with Detailed Output

```bash
abpvdev test -v detailed
```

### Run Specific Configuration

```bash
abpvdev test -c Release
```

## Project Detection

The test command automatically:
1. Finds all test projects (projects with `xunit`, `nunit`, or `mstest` references)
2. Builds the solution
3. Runs tests in all test projects

## Common Test Frameworks

AbpDevTools supports:
- **xUnit** (most common with ABP)
- **NUnit**
- **MSTest**

## Troubleshooting

### Tests Not Found

Make sure your test projects follow standard naming conventions:
- `*.Tests.csproj`
- `*.Test.csproj`
- `*.UnitTests.csproj`

### Build Errors

Run `abpvdev build` first to see detailed Test Timeouts

 build errors.

###Use `--verbosity detailed` to see more information about slow tests.
