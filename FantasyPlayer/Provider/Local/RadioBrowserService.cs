using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using FantasyPlayer.Config;
using Newtonsoft.Json.Linq;

namespace FantasyPlayer.Provider.Local
{
    /// <summary>
    /// Client for the radio-browser.info API (free, crowd-sourced internet radio directory).
    /// Search a query server ({cc}.api.radio-browser.info) and return the resolved stream URLs.
    /// </summary>
    public sealed class RadioBrowserService
    {
        private const string ApiBase = "https://de1.api.radio-browser.info/json";
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        public Task<List<RadioStation>> TopStationAsync(int limit = 50)
        {
            return GetStationsAsync($"{ApiBase}/stations/topvote/{limit}?hidebroken=true");
        }

        public Task<List<RadioStation>> SearchAsync(string query, int limit = 50)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Task.FromResult(new List<RadioStation>());
            }

            var encoded = Uri.EscapeDataString(query.Trim());
            return GetStationsAsync($"{ApiBase}/stations/search?name={encoded}&hidebroken=true&order=votes&reverse=true&limit={limit}");
        }

        private static async Task<List<RadioStation>> GetStationsAsync(string url)
        {
            var json = await Http.GetStringAsync(url);
            var array = JArray.Parse(json);
            var stations = new List<RadioStation>();
            foreach (var item in array)
            {
                var station = new RadioStation
                {
                    Name = item["name"]?.Value<string>() ?? string.Empty,
                    Url = item["url_resolved"]?.Value<string>() ?? item["url"]?.Value<string>() ?? string.Empty,
                    Country = item["country"]?.Value<string>() ?? string.Empty,
                    Codec = item["codec"]?.Value<string>() ?? string.Empty,
                    Bitrate = item["bitrate"]?.Value<int>() ?? 0,
                    Votes = item["votes"]?.Value<int>() ?? 0,
                    Tags = item["tags"]?.Value<string>() ?? string.Empty
                };
                if (!string.IsNullOrWhiteSpace(station.Url))
                {
                    stations.Add(station);
                }
            }

            return stations;
        }
    }
}