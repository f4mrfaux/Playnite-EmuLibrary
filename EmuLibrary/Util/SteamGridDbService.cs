using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Playnite.SDK;
using System.Linq;

namespace EmuLibrary.Util
{
    /// <summary>
    /// Service for interacting with the SteamGridDB API
    /// </summary>
    public class SteamGridDbService
    {
        private readonly ILogger _logger;
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        private readonly bool _isEnabled;
        
        // Cache of game names to IDs to avoid repeated API calls
        private readonly Dictionary<string, string> _gameNameCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        // Singleton instance
        private static SteamGridDbService _instance;
        public static SteamGridDbService Instance => _instance;
        
        /// <summary>
        /// Creates a new SteamGridDB service
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="apiKey">SteamGridDB API key</param>
        public SteamGridDbService(ILogger logger, string apiKey)
        {
            _logger = logger;
            _apiKey = apiKey;
            _isEnabled = !string.IsNullOrEmpty(_apiKey);
            
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            
            if (_isEnabled)
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            }
            
            _instance = this;
        }
        
        /// <summary>
        /// Checks if the service is configured with a valid API key
        /// </summary>
        public bool IsEnabled => _isEnabled;
        
        /// <summary>
        /// Search for games matching the provided name
        /// </summary>
        /// <param name="gameName">Game name to search for</param>
        /// <returns>List of matching games</returns>
        public async Task<List<SteamGridDbGame>> SearchGamesAsync(string gameName)
        {
            if (!_isEnabled)
            {
                _logger.Warn("SteamGridDB API is not enabled (no API key provided)");
                return new List<SteamGridDbGame>();
            }
            
            try
            {
                // Check cache first
                if (_gameNameCache.TryGetValue(gameName, out string cachedId))
                {
                    _logger.Debug($"Found cached SteamGridDB ID for {gameName}: {cachedId}");
                    var game = new SteamGridDbGame
                    {
                        Id = cachedId,
                        Name = gameName
                    };
                    return new List<SteamGridDbGame> { game };
                }
                
                // URL encode the game name
                var encodedName = Uri.EscapeDataString(gameName);
                var url = $"https://www.steamgriddb.com/api/v2/search/autocomplete/{encodedName}";
                
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.Error($"Failed to search SteamGridDB: {response.StatusCode} {response.ReasonPhrase}");
                    return new List<SteamGridDbGame>();
                }
                
                var content = await response.Content.ReadAsStringAsync();
                var searchResult = JsonConvert.DeserializeObject<SteamGridDbSearchResponse>(content);
                
                if (searchResult?.Data == null || !searchResult.Success)
                {
                    _logger.Warn($"No results found for {gameName} on SteamGridDB");
                    return new List<SteamGridDbGame>();
                }
                
                // Cache the first (best) result
                if (searchResult.Data.Any())
                {
                    var bestMatch = searchResult.Data.First();
                    _gameNameCache[gameName] = bestMatch.Id;
                    _logger.Info($"Added {gameName} to SteamGridDB cache with ID {bestMatch.Id}");
                }
                
                return searchResult.Data;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error searching SteamGridDB: {ex.Message}");
                return new List<SteamGridDbGame>();
            }
        }
        
        /// <summary>
        /// Find the best match for a game name
        /// </summary>
        /// <param name="gameName">Game name to search for</param>
        /// <returns>Best matching game or null if no match</returns>
        public async Task<SteamGridDbGame> FindBestMatchAsync(string gameName)
        {
            var results = await SearchGamesAsync(gameName);
            return results.FirstOrDefault();
        }
        
        /// <summary>
        /// Check if the given name matches a known game in SteamGridDB
        /// </summary>
        /// <param name="folderName">Original folder name</param>
        /// <param name="normalizedName">Normalized name to use if match found</param>
        /// <returns>True if a match was found</returns>
        public async Task<bool> TryMatchGameNameAsync(string folderName, out string normalizedName)
        {
            normalizedName = folderName;
            
            if (!_isEnabled)
            {
                return false;
            }
            
            try
            {
                var match = await FindBestMatchAsync(folderName);
                if (match != null)
                {
                    normalizedName = match.Name;
                    _logger.Info($"Matched folder '{folderName}' to SteamGridDB game '{match.Name}'");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error matching game name with SteamGridDB: {ex.Message}");
            }
            
            return false;
        }
    }
    
    /// <summary>
    /// Response from the SteamGridDB API search endpoint
    /// </summary>
    public class SteamGridDbSearchResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }
        
        [JsonProperty("data")]
        public List<SteamGridDbGame> Data { get; set; }
    }
    
    /// <summary>
    /// Game information from SteamGridDB
    /// </summary>
    public class SteamGridDbGame
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("release_date")]
        public string ReleaseDate { get; set; }
        
        [JsonProperty("types")]
        public List<string> Types { get; set; }
    }
}