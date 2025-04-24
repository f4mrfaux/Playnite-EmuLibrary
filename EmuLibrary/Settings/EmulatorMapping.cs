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
                
                // Special case for PC-type installers (PCInstaller, ISOInstaller)
                // They should show all available platforms, as they don't rely on emulator-specific platforms
                if (RomType == RomType.PCInstaller || RomType == RomType.ISOInstaller)
                {
                    // Get ALL available platforms from Playnite
                    var platforms = playnite.Database.Platforms
                        .Where(p => p != null && !string.IsNullOrEmpty(p.Name))
                        .Select(p => new EmulatedPlatform 
                        { 
                            Id = !string.IsNullOrEmpty(p.SpecificationId) ? p.SpecificationId : p.Id.ToString(),
                            Name = p.Name 
                        })
                        .ToList();
                        
                    // Make sure PC platform is at the top of the list
                    var pcPlatform = platforms.FirstOrDefault(p => p.Name == "PC" || p.Name == "Windows");
                    if (pcPlatform != null)
                    {
                        // Move PC platform to the beginning of the list
                        platforms.Remove(pcPlatform);
                        platforms.Insert(0, pcPlatform);
                    }
                    
                    return platforms;
                }
                
                // Original logic for other ROM types
                HashSet<string> validPlatforms = new HashSet<string>();

                if (EmulatorProfile != null)
                {
                    if (EmulatorProfile is CustomEmulatorProfile customProfile)
                    {
                        // Safely check customProfile.Platforms
                        var platforms = customProfile.Platforms;
                        if (platforms != null)
                        {
                            validPlatforms = new HashSet<string>(
                                playnite.Database.Platforms
                                .Where(p => p != null && platforms.Contains(p.Id))
                                .Select(p => p.SpecificationId)
                                .Where(id => !string.IsNullOrEmpty(id))
                            );
                        }
                    }
                    else if (EmulatorProfile is BuiltInEmulatorProfile builtInProfile && Emulator != null)
                    {
                        // Find the emulator and profile, applying null-checks along the way
                        var emulators = playnite.Emulation.Emulators;
                        var emulator = emulators?.FirstOrDefault(e => e.Id == Emulator.BuiltInConfigId);
                        var profile = emulator?.Profiles?.FirstOrDefault(p => p.Name == builtInProfile.Name);
                        var platforms = profile?.Platforms;

                        if (platforms != null)
                        {
                            validPlatforms = new HashSet<string>(platforms.Where(p => !string.IsNullOrEmpty(p)));
                        }
                    }
                }

                // Safely filter the list of platforms
                return playnite.Emulation.Platforms?
                    .Where(p => p != null && !string.IsNullOrEmpty(p.Id) && validPlatforms.Contains(p.Id))
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
                IEnumerable<string> imageExtensionsLower;
                if (RomType == RomType.PCInstaller)
                {
                    // For PC installers, we only support .exe files
                    imageExtensionsLower = new[] { "exe" };
                }
                else if (RomType == RomType.ISOInstaller)
                {
                    // For ISO installers, we support common disc image formats
                    imageExtensionsLower = new[] { "iso", "bin", "img", "cue", "nrg", "mds", "mdf" };
                }
                // Use emulator profile for other ROM types
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
