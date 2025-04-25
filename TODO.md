# TODO Features

## Enhanced Game Directory Detection

### Overview
Improve ISOInstaller and PCInstaller types' ability to detect and match game directories with similar but not exact names.

### Requirements

1. **Fuzzy Matching for Game Directories**
   - Implement Levenshtein distance algorithm for string similarity comparison
   - Set configurable threshold for matching similar directory names
   - Apply string normalization (remove special characters, normalize case)
   - Handle common name variations (e.g., with/without "The", year variations)

2. **Game Name Metadata Integration**
   - Integrate with SteamGridDB for game name verification
   - Use API to validate and correct game names based on directory names
   - Cache results to avoid excessive API calls
   - Add fallback mechanisms when API is unavailable

3. **Parent-Child Relationship Detection**
   - Improve detection of update and DLC folders within game directories
   - Better handle version numbers in directory and file names
   - Support various naming conventions for content updates

4. **UI/UX Considerations**
   - Show match confidence score in game properties
   - Allow manual correction of mismatched game names
   - Provide clear indicators for fuzzy-matched games

### Implementation Strategy

1. Add string extension methods for Levenshtein distance calculation
2. Implement name normalization functions
3. Create SteamGridDB API integration service
4. Update scanner classes to use the new matching algorithms
5. Add appropriate configuration options to settings

### Technical Considerations

- Balance performance with accuracy for large game libraries
- Consider caching mechanisms for large collections
- Implement proper error handling for network failures
- Respect API rate limits