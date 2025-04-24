# Playnite Metadata Integration

## Overview

This document explains the integration between EmuLibrary and Playnite's built-in metadata system.

## Using Playnite's Metadata System

EmuLibrary now leverages Playnite's built-in metadata system for game information, covers, and other assets. This approach offers several advantages:

1. **Unified Metadata Experience**: Uses the same metadata providers as other Playnite games
2. **Better Integration**: Takes advantage of all installed metadata extensions in Playnite
3. **Enhanced Metadata Quality**: Access to multiple metadata sources through Playnite extensions
4. **Simplified Maintenance**: No need to maintain API keys or custom integration code

## How It Works

1. **Metadata Support**: The "Auto-download metadata for imported games" setting indicates your preference for metadata, but the actual download needs to be triggered manually through Playnite's UI after games are imported
2. **Manual Metadata Download**:
   - Bulk download: Select multiple games, go to Main menu > Library > Download metadata
   - Single game: Right-click a game, select Edit, and click the "Download Metadata" button

## Available Metadata

Playnite's metadata system provides rich game information including:
- Game covers and backgrounds
- Game descriptions
- Release dates
- Genre and tags
- Developer and publisher information
- Platform details
- Community ratings

## Recommended Metadata Extensions

For best results with EmuLibrary games, install these extensions from Playnite's Add-on browser:
- SteamGridDB for game images and covers
- IGDB for comprehensive game data
- Store-specific extensions (GOG, Steam, etc.) if you use those platforms

## Technical Implementation

- Game metadata objects created by EmuLibrary scanners include a clean game name for better matching
- EmuLibrary doesn't set other metadata fields to let Playnite's metadata system fill them
- The `AutoRequestMetadata` setting indicates your preference for metadata but doesn't trigger automatic downloads
- For installed games, Playnite's metadata system is used during the metadata download process

## Changes in Version 1.0.0

- Removed custom SteamGridDB integration in favor of Playnite's built-in metadata system
- Removed SteamGridDB API key and matching settings
- Updated documentation to reflect the use of Playnite's metadata system
- Improved metadata handling during game import