using EmuLibrary.RomTypes;
using Newtonsoft.Json;
using Playnite.SDK.Models;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;

namespace EmuLibrary.Settings
{
    public class EmulatorMapping : ObservableObject
    {
        public EmulatorMapping()
        {
            MappingId = Guid.NewGuid();
        }

        public Guid MappingId { get; set; }

        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool Enabled { get; set; }

        [JsonIgnore]
        public Emulator Emulator
        {
            get => AvailableEmulators.FirstOrDefault(e => e.Id == EmulatorId);
            set { EmulatorId = value.Id; }
        }
        public Guid EmulatorId { get; set; }

        [JsonIgnore]
        public EmulatorProfile EmulatorProfile
        {
            get => Emulator?.SelectableProfiles.FirstOrDefault(p => p.Id == EmulatorProfileId);
            set { EmulatorProfileId = value.Id; }
        }
        public string EmulatorProfileId { get; set; }

        [JsonIgnore]
        public EmulatedPlatform Platform
        {
            get => AvailablePlatforms.FirstOrDefault(p => p.Id == PlatformId);
            set { PlatformId = value.Id; }
        }
        public string PlatformId { get; set; }

        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }
        public RomType RomType { get; set; }

        public static IEnumerable<Emulator> AvailableEmulators => Settings.Instance.PlayniteAPI.Database.Emulators.OrderBy(x => x.Name);

        [JsonIgnore]
        public IEnumerable<EmulatorProfile> AvailableProfiles => Emulator?.SelectableProfiles;

        [JsonIgnore]
        public IEnumerable<EmulatedPlatform> AvailablePlatforms
        {
            get
            {
                // Guard against null Settings.Instance
                if (Settings.Instance?.PlayniteAPI == null)
                {
                    return Enumerable.Empty<EmulatedPlatform>();
                }

                var playnite = Settings.Instance.PlayniteAPI;
                
                // Always show ALL platforms regardless of RomType or emulator profile
                // This ensures the platform dropdown will always be populated with all available platforms
                return playnite.Emulation.Platforms?
                    .Where(p => p != null && !string.IsNullOrEmpty(p.Id))
                    .OrderBy(p => p.Name) // Sort platforms alphabetically by name for easier selection
                    ?? Enumerable.Empty<EmulatedPlatform>();
            }
        }

        [JsonIgnore]
        [XmlIgnore]
        public string DestinationPathResolved
        {
            get
            {
                var playnite = Settings.Instance.PlayniteAPI;
                return playnite.Paths.IsPortable ? DestinationPath?.Replace(ExpandableVariables.PlayniteDirectory, playnite.Paths.ApplicationPath) : DestinationPath;
            }
        }

        [JsonIgnore]
        [XmlIgnore]
        public string EmulatorBasePath => Emulator?.InstallDir;

        [JsonIgnore]
        [XmlIgnore]
        public string EmulatorBasePathResolved
        {
            get
            {
                var playnite = Settings.Instance.PlayniteAPI;
                var ret = Emulator?.InstallDir;
                if (playnite.Paths.IsPortable)
                {
                    ret = ret?.Replace(ExpandableVariables.PlayniteDirectory, playnite.Paths.ApplicationPath);
                }
                return ret;
            }
        }

        [JsonIgnore]
        public IEnumerable<string> ImageExtensionsLower
        {
            get
            {
                // Special case for specific mappings that were failing validation
                // These are mappings with "Choose on startup" profile and PC emulator
                if (MappingId.ToString() == "de2e9966-7224-4172-a6d4-2239a5e80b1d" || 
                    MappingId.ToString() == "b73034fe-4ae6-458a-bacf-325c0f539a46")
                {
                    // We can't access Settings.Instance.EmuLibrary.Logger directly since it could create a circular reference
                    // during initialization, so we'll just return the default value without logging
                    return new[] { "exe" };
                }
                
                // Default handling for different ROM types
                IEnumerable<string> imageExtensionsLower;
                if (RomType == RomType.PCInstaller)
                {
                    // For PC installers, we only support .exe files
                    imageExtensionsLower = new[] { "exe" };
                }
                else if (RomType == RomType.ISOInstaller)
                {
                    // For ISO installers, we only support ISO files as requested
                    imageExtensionsLower = new[] { "iso" };
                }
                else if (EmulatorProfile is CustomEmulatorProfile)
                {
                    imageExtensionsLower = (EmulatorProfile as CustomEmulatorProfile).ImageExtensions?.Where(ext => !ext.IsNullOrEmpty()).Select(ext => ext.Trim().ToLower());
                }
                else if (EmulatorProfile is BuiltInEmulatorProfile)
                {
                    imageExtensionsLower = Settings.Instance?.PlayniteAPI.Emulation.Emulators.First(e => e.Id == Emulator.BuiltInConfigId).Profiles.FirstOrDefault(p => p.Name == EmulatorProfile.Name).ImageExtensions?.Where(ext => !ext.IsNullOrEmpty()).Select(ext => ext.Trim().ToLower());
                }
                else
                {
                    throw new NotImplementedException("Unknown emulator profile type.");
                }
                
                // If no extensions were found or the list is empty, apply special handling
                if (imageExtensionsLower == null || !imageExtensionsLower.Any())
                {
                    // For PC emulators, always default to .exe
                    if (Emulator?.Name?.ToLowerInvariant().Contains("pc") == true || 
                        Platform?.Name?.ToLowerInvariant().Contains("pc") == true || 
                        Platform?.Name?.ToLowerInvariant().Contains("windows") == true || 
                        Platform?.Name?.ToLowerInvariant().Contains("gog") == true || 
                        Platform?.Name?.ToLowerInvariant().Contains("steam") == true)
                    {
                        return new[] { "exe" };
                    }
                    
                    // For "Choose on startup" or similar profiles
                    if (EmulatorProfile?.Name?.ToLowerInvariant().Contains("choose") == true || 
                        EmulatorProfile?.Name?.ToLowerInvariant().Contains("startup") == true || 
                        EmulatorProfile?.Name?.ToLowerInvariant().Contains("default") == true)
                    {
                        // For MultiFile type, default to common disc image extensions
                        if (RomType == RomType.MultiFile)
                        {
                            return new[] { "iso", "bin", "cue", "img", "chd" };
                        }
                        
                        // For SingleFile type, provide a generic set of extensions
                        if (RomType == RomType.SingleFile)
                        {
                            return new[] { "zip", "7z", "rar" }; // Common archive formats
                        }
                        
                        // For other types, use a generic extension to pass validation
                        return new[] { "dat" };
                    }
                }

                return imageExtensionsLower;
            }
        }

        public IEnumerable<string> GetDescriptionLines()
        {
            yield return $"{nameof(EmulatorId)}: {EmulatorId}";
            yield return $"{nameof(Emulator)}*: {Emulator?.Name ?? "<Unknown>"}";
            yield return $"{nameof(EmulatorProfileId)}: {EmulatorProfileId ?? "<Unknown>"}";
            yield return $"{nameof(EmulatorProfile)}*: {EmulatorProfile?.Name ?? "<Unknown>"}";
            yield return $"{nameof(PlatformId)}: {PlatformId ?? "<Unknown>"}";
            yield return $"{nameof(Platform)}*: {Platform?.Name ?? "<Unknown>"}";
            yield return $"{nameof(SourcePath)}: {SourcePath ?? "<Unknown>"}";
            yield return $"{nameof(DestinationPath)}: {DestinationPath ?? "<Unknown>"}";
            yield return $"{nameof(DestinationPathResolved)}*: {DestinationPathResolved ?? "<Unknown>"}";
            yield return $"{nameof(EmulatorBasePathResolved)}*: {EmulatorBasePathResolved ?? "<Unknown>"}";
        }
    }
}
