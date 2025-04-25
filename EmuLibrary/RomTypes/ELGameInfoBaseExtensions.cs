using EmuLibrary.RomTypes;
using EmuLibrary.RomTypes.PCInstaller;
using EmuLibrary.RomTypes.ISOInstaller;
using Playnite.SDK.Models;
using System.Collections.Generic;

internal static class ELGameInfoBaseExtensions
{
    // Dictionary to store metadata properties for each game
    private static Dictionary<string, Dictionary<string, object>> _metadataProperties = 
        new Dictionary<string, Dictionary<string, object>>();
        
    static public ELGameInfo GetELGameInfo(this Game game)
    {
        return ELGameInfo.FromGame<ELGameInfo>(game);
    }

    static public ELGameInfo GetELGameInfo(this GameMetadata game)
    {
        return ELGameInfo.FromGameMetadata<ELGameInfo>(game);
    }
    
    static public void SetELGameInfo(this Game game, ELGameInfo gameInfo)
    {
        game.GameId = gameInfo.AsGameId();
    }
    
    // Extension methods to simulate Properties dictionary on GameMetadata
    static public Dictionary<string, object> GetProperties(this GameMetadata metadata)
    {
        string key = metadata.GameId;
        if (!_metadataProperties.ContainsKey(key))
        {
            _metadataProperties[key] = new Dictionary<string, object>();
        }
        return _metadataProperties[key];
    }
    
    static public void AddProperty(this GameMetadata metadata, string key, object value)
    {
        var props = metadata.GetProperties();
        props[key] = value;
    }
    
    static public object GetProperty(this GameMetadata metadata, string key)
    {
        var props = metadata.GetProperties();
        return props.ContainsKey(key) ? props[key] : null;
    }
}