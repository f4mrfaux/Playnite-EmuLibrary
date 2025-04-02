# Project Directory Structure

This document provides an overview of the EmuLibrary project structure to help developers navigate and understand the codebase.

## Root Directory

| File/Directory | Description |
|----------------|-------------|
| `CHANGELOG.md` | Tracks changes to the project in a chronological order |
| `EmuLibrary.sln` | Visual Studio solution file that references all project files |
| `EmuLibrary/` | Main project directory containing source code |
| `LICENSE` | Project license information |
| `README.md` | Main project documentation and usage guide |
| `manifest.yaml` | Playnite extension manifest for the plugin |
| `toolbox/` | Contains Playnite SDK files and templates for development |
| `DIRECTORY.md` | This file - an overview of the project structure |

## Main Directories

- **EmuLibrary/**: Contains all the source code for the plugin
  - Core plugin functionality and integration with Playnite
  - Various ROM type implementations
  - Settings and utility classes
  
- **toolbox/**: Contains Playnite SDK and development tools
  - SDK libraries and references
  - Templates for extension development
  
See the README.md in each directory for more specific information about its contents.