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
                var playnite = Settings.Instance.PlayniteAPI;
                HashSet<string> validPlatforms;

                if (EmulatorProfile is CustomEmulatorProfile)
                {
                    var customProfile = EmulatorProfile as CustomEmulatorProfile;
                    validPlatforms = new HashSet<string>(playnite.Database.Platforms.Where(p => customProfile.Platforms.Contains(p.Id)).Select(p => p.SpecificationId));
                }
                else if (EmulatorProfile is BuiltInEmulatorProfile)
                {
                    var builtInProfile = (EmulatorProfile as BuiltInEmulatorProfile);
                    validPlatforms = new HashSet<string>(
                        playnite.Emulation.Emulators
                        .FirstOrDefault(e => e.Id == Emulator.BuiltInConfigId)?
                        .Profiles
                        .FirstOrDefault(p => p.Name == builtInProfile.Name)?
                        .Platforms
                        );
                }
                else
                {
                    validPlatforms = new HashSet<string>();
                }

                return Settings.Instance.PlayniteAPI.Emulation.Platforms.Where(p => validPlatforms.Contains(p.Id));
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
                // Default set of extensions to use if none are found
                string[] defaultExtensions = { ".iso", ".bin", ".exe", ".img" };
                
                try
                {
                    if (EmulatorProfile is CustomEmulatorProfile)
                    {
                        var extensions = (EmulatorProfile as CustomEmulatorProfile).ImageExtensions?
                            .Where(ext => !ext.IsNullOrEmpty())
                            .Select(ext => ext.Trim().ToLower());
                            
                        imageExtensionsLower = extensions != null && extensions.Any() 
                            ? extensions 
                            : defaultExtensions;
                    }
                    else if (EmulatorProfile is BuiltInEmulatorProfile)
                    {
                        try
                        {
                            var emulator = Settings.Instance?.PlayniteAPI.Emulation.Emulators
                                .FirstOrDefault(e => e.Id == Emulator?.BuiltInConfigId);
                                
                            if (emulator != null)
                            {
                                var profile = emulator.Profiles.FirstOrDefault(p => p.Name == EmulatorProfile.Name);
                                if (profile != null)
                                {
                                    var extensions = profile.ImageExtensions?
                                        .Where(ext => !ext.IsNullOrEmpty())
                                        .Select(ext => ext.Trim().ToLower());
                                        
                                    imageExtensionsLower = extensions != null && extensions.Any() 
                                        ? extensions 
                                        : defaultExtensions;
                                }
                                else
                                {
                                    imageExtensionsLower = defaultExtensions;
                                }
                            }
                            else
                            {
                                imageExtensionsLower = defaultExtensions;
                            }
                        }
                        catch
                        {
                            imageExtensionsLower = defaultExtensions;
                        }
                    }
                    else
                    {
                        // Fallback for any unknown profile type
                        imageExtensionsLower = defaultExtensions;
                    }
                }
                catch
                {
                    // Final fallback in case of any errors
                    imageExtensionsLower = defaultExtensions;
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
