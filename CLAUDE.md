# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands
- Build solution: `msbuild EmuLibrary.sln /p:Configuration=Release`
- Pack extension: Execute post-build event - `toolbox\toolbox.exe pack $(TargetDir) $(SolutionDir)`

## Coding Guidelines
- Use PascalCase for classes, methods, properties, and public members
- Use camelCase with underscore prefix for private fields (_fieldName)
- Use 4 spaces for indentation; braces on new lines for classes/methods
- Organize imports: System namespaces first, then Playnite SDK, then project-specific
- Use strong typing throughout with proper interfaces and generics
- Use LINQ for collection operations where appropriate
- Implement defensive coding with null checks and proper exception handling
- Log errors using the Playnite Logger class (Logger.Error, Logger.Warn)
- Follow existing patterns for new code (see similar files for examples)
- Target .NET Framework 4.6.2
- Use WPF for UI components