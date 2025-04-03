# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Assistant Name
- This assistant should be referred to as "Marvin" in this project

## Build Commands
- Build (Windows): `dotnet build -c Release` 
- Build (Linux): `./build.sh`
- Syntax check (Linux): `./check-syntax.sh`
- Clean build: `./build-clean.sh` (Linux) or `.\build-clean.ps1` (Windows)
- Download dependencies: `.\download-dependencies.ps1`

## Project Key Decisions
- GOG Installer functionality has been merged into PC Installer for simplification
- GogInstaller RomType enum value is preserved for backward compatibility, but uses PcInstallerGameInfo and PcInstallerScanner
- UI styling must explicitly set foregrounds/backgrounds for good contrast in both light and dark themes

## Critical Dependencies
- protobuf-net: Must use version 2.4.0 exactly (not 2.4.6) to match Playnite's version
- LibHac: Version 0.7.0 for Switch game support
- All dependencies need <CopyLocal>true</CopyLocal> and <Private>true</Private> in the .csproj

## Code Style Guidelines
- Naming: PascalCase for classes, methods, properties; camelCase for variables (prefix private fields with underscore)
- Formatting: Braces on new lines, 4-space indentation
- Error handling: Use try/catch blocks with logger.Error() for exceptions
- File organization: Each RomType has its own folder with specific implementations
- Design pattern: Follow the scanner/controller pattern for new ROM types
- Import format: System imports first, then Playnite SDK, then project namespaces

## Error Handling
- Add fallbacks for every operation that could throw an exception
- Use default values in case of missing/null data
- Log errors with appropriate severity (Warn vs Error)

## Architecture Notes
- Follow the RomType pattern for new implementations (see DEVELOPER.md)
- Custom ROM types must implement GameInfo, Scanner, InstallController, and UninstallController
- Use logger for all important operations (Logger.Info, Logger.Warn, Logger.Error)
- Handle cancellation tokens properly in long-running operations