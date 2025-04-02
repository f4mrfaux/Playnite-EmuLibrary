# EmuLibrary Plugin Architecture

This document provides a comprehensive overview of the EmuLibrary plugin architecture, explaining how the various components work together.

## High-Level Architecture

The EmuLibrary plugin follows a modular architecture with several key layers:

1. **Core Integration Layer**
   - `EmuLibrary.cs`: Main plugin class that integrates with Playnite
   - `IEmuLibrary.cs`: Interface defining the core plugin functionality

2. **ROM Type System**
   - `RomType.cs`: Enum defining supported types
   - `RomTypeInfoAttribute.cs`: Metadata for types
   - `RomTypeScanner.cs`: Base scanning functionality
   - Type-specific implementations in subdirectories

3. **Configuration System**
   - `Settings/`: User preferences and mapping configuration
   - UI for settings management

4. **Utility Layer**
   - `Util/`: Helper classes and utilities
   - `PlayniteCommon/`: Shared functionality

## Component Interactions

```
┌─────────────────┐      ┌─────────────┐
│    Playnite     │◄────►│  EmuLibrary  │
└─────────────────┘      └──────┬──────┘
                                │
                         ┌──────┴──────┐
                         │   Settings   │
                         └──────┬──────┘
                                │
           ┌────────────────────┼────────────────────┐
           │                     │                    │
    ┌──────▼───────┐     ┌──────▼───────┐     ┌──────▼───────┐
    │  SingleFile   │     │  MultiFile   │     │  PcInstaller  │
    └──────┬───────┘     └──────┬───────┘     └──────┬───────┘
           │                     │                    │
    ┌──────▼───────┐     ┌──────▼───────┐     ┌──────▼───────┐
    │  Scanner     │     │  Scanner     │     │  Scanner     │
    │  GameInfo    │     │  GameInfo    │     │  GameInfo    │
    │  Controllers │     │  Controllers │     │  Controllers │
    └──────────────┘     └──────────────┘     └──────┬───────┘
                                                     │
                                              ┌──────▼───────┐
                                              │   Handlers   │
                                              └──────────────┘
```

## Key Design Patterns

EmuLibrary employs several design patterns:

1. **Factory Pattern**
   - `RomTypeScanner` creates appropriate scanner instances
   - `ArchiveHandlerFactory` creates appropriate archive handlers

2. **Strategy Pattern**
   - Different ROM type implementations provide specialized behavior
   - Different archive handlers handle specific formats

3. **Template Method Pattern**
   - Base classes define the algorithm skeleton
   - Subclasses override specific steps

4. **Observer Pattern**
   - Installation progress notifications
   - Event-based communication between components

## Extensibility

The plugin architecture is designed for extensibility:

1. **Adding ROM Types**
   - Create a new subdirectory under `RomTypes/`
   - Implement the required classes
   - Add the type to the `RomType` enum with an attribute

2. **Adding Archive Handlers**
   - Implement the `IArchiveHandler` interface
   - Register the handler in `ArchiveHandlerFactory`

3. **Adding UI Components**
   - Extend the settings view in `SettingsView.xaml`
   - Add properties to the `Settings` class

## Data Flow

### Game Detection Flow:

1. Playnite calls `EmuLibrary.GetGames()`
2. `EmuLibrary` iterates through enabled mappings
3. For each mapping, the appropriate scanner is called
4. Scanners detect games and create `GameMetadata` objects
5. Metadata is returned to Playnite for library integration

### Installation Flow:

1. Playnite calls `EmuLibrary.GetInstallActions()`
2. `EmuLibrary` returns the appropriate controller
3. Playnite calls `InstallController.Install()`
4. Controller copies files or executes installers
5. Game status is updated in Playnite
6. Installation events are fired for progress reporting

## Threading Model

The plugin uses a mix of synchronous and asynchronous operations:

- Scanning is primarily synchronous for simplicity
- Installation and uninstallation use async/await pattern
- UI updates are marshaled to the UI thread using UIDispatcher
- Long-running operations use Task.Run with cancellation support

## Error Handling

The plugin implements robust error handling:

- Exceptions are caught and logged
- Users are notified of errors via Playnite notifications
- Fallback mechanisms are implemented for critical operations
- Network errors are handled with appropriate retries and timeouts

## Performance Considerations

Several optimizations are implemented:

- Caching for installer detection and game naming
- Efficient file enumeration with `SafeFileEnumerator`
- Memory management with cache size limits
- Optimized archive handling with external tools