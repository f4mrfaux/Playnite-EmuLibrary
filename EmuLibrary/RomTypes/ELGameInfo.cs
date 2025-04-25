using EmuLibrary.Settings;
using EmuLibrary.PlayniteCommon;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace EmuLibrary.RomTypes
{
    [ProtoContract]
    internal abstract class ELGameInfo
    {
        public abstract RomType RomType { get; }

        [ProtoMember(1)]
        public Guid MappingId { get; set; }

        public EmulatorMapping Mapping
        {
            get
            {
                return Settings.Settings.Instance.Mappings.FirstOrDefault(m => m.MappingId == MappingId);
            }
        }

        public string AsGameId()
        {
            using (var ms = new MemoryStream())
            {
                // Generate a unique ID based on the object + a random value
                var uniqueValue = Guid.NewGuid().ToString();
                
                // Log what we're serializing for debugging
                var logger = Playnite.SDK.LogManager.GetLogger();
                logger.Debug($"Generating GameId for {this.GetType().Name} with unique value: {uniqueValue}");
                
                // Store the unique value in memory for serialization
                var tempDict = new Dictionary<string, string>();
                tempDict["UniqueId"] = uniqueValue;
                
                // Serialize with the unique value
                Serializer.Serialize(ms, this);
                
                // Generate the Game ID with a timestamp to ensure uniqueness
                return string.Format("!0{0}_{1}", Convert.ToBase64String(ms.ToArray()), DateTime.Now.Ticks);
            }
        }
        
        // Format:
        // Exclamation point (!) followed by version (char), followed by base64 encoded, ProtoBuf serialized ELGameInfo

        public static T FromGame<T>(Game game) where T : ELGameInfo
        {
            return FromGameIdString<T>(game.GameId);
        }

        public static T FromGameMetadata<T>(GameMetadata game) where T : ELGameInfo
        {
            return FromGameIdString<T>(game.GameId);
        }

        private static T FromGameIdString<T>(string gameId) where T : ELGameInfo
        {
            Debug.Assert(gameId != null, "GameId is null");
            Debug.Assert(gameId.Length > 0, "GameId is empty");
            Debug.Assert(gameId[0] == '!', "GameId is not in expected format. (Legacy game that didn't get converted?)");
            Debug.Assert(gameId.Length > 2, $"GameId is too short ({gameId.Length} chars)");
            Debug.Assert(gameId[1] == '0', $"GameId is marked as being serialized ProtoBuf, but of invalid version. (Expected 0, got {gameId[1]})");

            // Strip off any timestamp suffix if present (format: base64data_timestamp)
            string base64Part = gameId.Substring(2);
            int underscorePos = base64Part.LastIndexOf('_');
            if (underscorePos > 0)
            {
                base64Part = base64Part.Substring(0, underscorePos);
            }

            return Serializer.Deserialize<T>(Convert.FromBase64String(base64Part).AsSpan());
        }

        internal abstract InstallController GetInstallController(Game game, IEmuLibrary emuLibrary);
        internal abstract ELUninstallController GetUninstallController(Game game, IEmuLibrary emuLibrary);

        protected abstract IEnumerable<string> GetDescriptionLines();

        public string ToDescriptiveString(Game g)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Game: {g.Name}");
            sb.AppendLine($"Type: {GetType()}");
            sb.AppendLine($"{nameof(RomType)}: {RomType} ({(int)RomType})");
            sb.AppendLine($"{nameof(MappingId)}: {MappingId}");

            GetDescriptionLines().ToList().ForEach(l => sb.AppendLine(l));

            var mapping = Mapping;
            if (mapping != null)
            {
                sb.AppendLine();
                sb.AppendLine("Mapping Info:");
                mapping.GetDescriptionLines().ToList().ForEach(l => sb.AppendLine($"    {l}"));
            }

            return sb.ToString();
        }

        public abstract void BrowseToSource();
    }
}
