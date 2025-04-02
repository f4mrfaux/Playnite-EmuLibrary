# Settings Directory

This directory contains classes related to configuring and storing plugin settings, as well as the user interface for modifying these settings.

## Key Files

| File | Description |
|------|-------------|
| `Settings.cs` | Main settings class that implements `ISettings` and stores all plugin configuration options. |
| `SettingsV0.cs` | Legacy settings format for backward compatibility with older versions. |
| `SettingsView.xaml` | XAML definition of the settings UI shown in Playnite. |
| `SettingsView.xaml.cs` | Code-behind for the settings UI, handling user interactions. |
| `EmulatorMapping.cs` | Class representing a mapping between an emulator, platform, and source path. |
| `PathValidator.cs` | Utility class for validating path inputs in settings. |

## Settings Options

The `Settings` class includes configuration options for:

- **General Settings**: Scanning behavior, notification preferences
- **PC Installer Settings**: Auto-detection, default installation location, etc.
- **Metadata Settings**: Options for metadata matching and download
- **UI Settings**: Display preferences for installation dialogs
- **Mappings**: Collection of `EmulatorMapping` objects that define what to scan and how to handle it

## Settings UI

The settings UI is implemented in `SettingsView.xaml` and provides:

- A grid for managing emulator mappings
- Checkboxes for various plugin options
- Help panels with guidance for new users
- Warning panels for important information
- File/folder browse dialogs for path selection

## Settings Migration

The plugin includes a settings migration system that:

1. Attempts to load settings using the current format
2. If that fails, tries to load using the legacy format (`SettingsV0`)
3. Converts legacy settings to the current format if needed

This ensures backward compatibility when updating the plugin.