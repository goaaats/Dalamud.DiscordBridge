﻿using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Newtonsoft.Json.Linq;

namespace Dalamud.DiscordBridge.XivApi
{
    static class XivApiClient
    {
        private const string URL = "https://xivapi.com/";

        private static readonly ConcurrentDictionary<string, JObject> CachedResponses = new ConcurrentDictionary<string, JObject>();

        /// <summary>
        /// Returns the searched character, or null if not found.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="world"></param>
        /// <returns></returns>
        public static async Task<CharacterResult> GetCharacterSearch(string name, string world)
        {
            var res = await Get("character/search" + $"?name={name}&server={world}");

            if (res.Results.Count > 0)
            {
                return new CharacterResult
                {
                    AvatarUrl = res.Results[0].Avatar,
                    LodestoneId = res.Results[0].ID
                };
            }

            return null;
        }

        public static async Task<JObject> Search(string query, string indexes, int limit = 100, bool exact = false) {
            query = System.Net.WebUtility.UrlEncode(query);

            var queryString = $"?string={query}&indexes={indexes}&limit={limit}";
            if (exact)
            {
                queryString += "&string_algo=match";
            }

            return await Get("search" + queryString);
        }

        private static async Task<dynamic> Get(string endpoint, bool noCache = false)
        {
            PluginLog.Verbose("XIVAPI FETCH: {0}", endpoint);

            if (CachedResponses.TryGetValue(endpoint, out var val) && !noCache)
                return val;

            var client = new HttpClient();
            var response = await client.GetAsync(URL + endpoint);
            var result = await response.Content.ReadAsStringAsync();

            var obj = JObject.Parse(result);

            if (!noCache)
                CachedResponses.TryAdd(endpoint, obj);

            return obj;
        }
    }
}