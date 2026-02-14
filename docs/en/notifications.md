---
id: notifications
title: Notifications
---

# Notifications

AbpDevTools can send desktop notifications when long-running operations complete. This allows you to work on other tasks while waiting for builds, migrations, or application startup.

## Platform Support

| Platform | Support |
|----------|---------|
| Windows | Full support |
| macOS | Full support |
| Linux | Not supported |

## Enable Notifications

To enable desktop notifications:

```bash
abpvdev enable-notifications
```

## Disable Notifications

To disable desktop notifications:

```bash
abpvdev disable-notifications
```

## Notification Events

Notifications are sent when these operations complete:

- **Migration** completes
- **Build** completes
- **Run** completes

## How It Works

When enabled, AbpDevTools will:
1. Monitor the operation progress
2. Send a desktop notification when the operation finishes
3. Include success/failure status in the notification

## Example

### Build Notification

When running:

```bash
abpvdev build
```

You'll receive a notification like:

```
✅ Build Complete
MyApp.sln - Build succeeded (2m 34s)
```

### Migration Notification

When running:

```bash
abpvdev migrate
```

You'll receive:

```
✅ Migration Complete
Applied 3 migrations (15s)
```

## Troubleshooting

### Notifications Not Working

1. **Check if enabled**: Run `abpvdev enable-notifications`
2. **Platform support**: Notifications only work on Windows and macOS
3. **System permissions**: Ensure desktop notifications are allowed in your OS settings

### Notifications Don't Appear

- Check Windows Focus Assist or macOS Do Not Disturb settings
- Ensure the notification app has permissions

## Configuration

Notifications are enabled per-machine. The setting is stored in your user configuration.

## Best Practices

- Enable notifications for long-running operations
- Use in combination with watch mode for development
- Combine with virtual environments for multi-solution workflows
