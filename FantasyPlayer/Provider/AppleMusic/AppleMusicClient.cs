using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FantasyPlayer.Provider.Common;

namespace FantasyPlayer.Provider.AppleMusic
{
    public sealed class AppleMusicClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _developerToken;
        private readonly string? _musicUserToken;

        private bool _disposed;
        private string? _lastError;

        public string? LastError => _lastError;

        public AppleMusicClient(string developerToken, string? musicUserToken = null)
        {
            _http = new HttpClient { BaseAddress = new Uri("https://api.music.apple.com") };
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {developerToken}");

            if (!string.IsNullOrEmpty(musicUserToken))
            {
                _musicUserToken = musicUserToken;
                _http.DefaultRequestHeaders.Add("Media-User-Token", musicUserToken);
            }
            else
            {
                _musicUserToken = null;
            }

            _developerToken = developerToken;
        }

        public async Task<AppleMusicUser?> GetUserProfile()
        {
            try
            {
                if (string.IsNullOrEmpty(_musicUserToken))
                    return null;

                var response = await _http.GetAsync("/v1/me/library/playlists");
                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                return new AppleMusicUser
                {
                    IsAuthenticated = true
                };
            }
            catch (Exception e)
            {
                _lastError = e.Message;
                return null;
            }
        }

        public async Task<CurrentlyPlayingContext?> GetCurrentPlayback()
        {
            try
            {
                var response = await _http.GetAsync("/v1/me/recent/played/tracks?limit=1");
                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var data = doc.RootElement.GetProperty("data");

                if (data.GetArrayLength() == 0)
                    return null;

                var track = data[0];
                var attributes = track.GetProperty("attributes");

                return new CurrentlyPlayingContext
                {
                    Track = ParseTrack(track, attributes)
                };
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<QueueItem>> GetQueue()
        {
            var items = new List<QueueItem>();
            try
            {
                var response = await _http.GetAsync("/v1/me/recent/played/tracks?limit=50");
                if (!response.IsSuccessStatusCode)
                    return items;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var data = doc.RootElement.GetProperty("data");

                foreach (var track in data.EnumerateArray())
                {
                    var attributes = track.GetProperty("attributes");
                    items.Add(ParseQueueItem(track, attributes));
                }
            }
            catch
            {
            }
            return items;
        }

        public async Task<List<PlaylistStruct>> GetPlaylists()
        {
            var playlists = new List<PlaylistStruct>();
            try
            {
                var endpoint = string.IsNullOrEmpty(_musicUserToken)
                    ? "/v1/catalog/us/playlists?ids=pl.u-6mo4465tzJ9Lg6"
                    : "/v1/me/library/playlists?limit=50";

                var response = await _http.GetAsync(endpoint);
                if (!response.IsSuccessStatusCode)
                    return playlists;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var data = doc.RootElement.GetProperty("data");

                foreach (var playlist in data.EnumerateArray())
                {
                    var attributes = playlist.GetProperty("attributes");
                    var id = playlist.GetProperty("id").GetString() ?? "";
                    var trackCount = attributes.TryGetProperty("trackCount", out var tc) ? tc.GetInt32() : 0;

                    playlists.Add(new PlaylistStruct
                    {
                        Id = id,
                        Name = attributes.GetProperty("name").GetString() ?? "",
                        Description = attributes.TryGetProperty("description", out var desc) ? desc.GetProperty("standard").GetString() ?? "" : "",
                        ImageUrl = attributes.TryGetProperty("artwork", out var artwork)
                            ? ParseArtworkUrl(artwork)
                            : "",
                        TrackCount = trackCount,
                        Owner = attributes.TryGetProperty("curatorName", out var curator) ? curator.GetString() ?? "" : ""
                    });
                }
            }
            catch
            {
            }
            return playlists;
        }

        public async Task<PlaylistTrackList> GetPlaylistTracks(string playlistId)
        {
            var result = new PlaylistTrackList { Tracks = new List<PlaylistItem>() };
            try
            {
                var endpoint = $"/v1/catalog/us/playlists/{playlistId}";
                var response = await _http.GetAsync(endpoint);
                if (!response.IsSuccessStatusCode)
                    return result;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var data = doc.RootElement.GetProperty("data");
                if (data.GetArrayLength() == 0)
                    return result;

                var playlist = data[0];
                var attributes = playlist.GetProperty("attributes");
                result.Playlist = new PlaylistStruct
                {
                    Id = playlist.GetProperty("id").GetString() ?? "",
                    Name = attributes.GetProperty("name").GetString() ?? "",
                    Description = attributes.TryGetProperty("description", out var desc) ? desc.GetProperty("standard").GetString() ?? "" : "",
                    ImageUrl = attributes.TryGetProperty("artwork", out var artwork) ? ParseArtworkUrl(artwork) : "",
                    TrackCount = attributes.TryGetProperty("trackCount", out var tc) ? tc.GetInt32() : 0
                };

                if (attributes.TryGetProperty("playParams", out var pp) && pp.TryGetProperty("globalId", out var globalId))
                {
                    var tracksResponse = await _http.GetAsync($"/v1/catalog/us/playlists/{playlistId}/tracks?limit=100");
                    if (tracksResponse.IsSuccessStatusCode)
                    {
                        var tracksJson = await tracksResponse.Content.ReadAsStringAsync();
                        using var tracksDoc = JsonDocument.Parse(tracksJson);
                        var tracksData = tracksDoc.RootElement.GetProperty("data");

                        foreach (var track in tracksData.EnumerateArray())
                        {
                            var trackAttr = track.GetProperty("attributes");
                            result.Tracks.Add(ParsePlaylistItem(track, trackAttr, playlistId));
                        }
                    }
                }
            }
            catch
            {
            }
            return result;
        }

        public async Task<List<QueueItem>> SearchTracks(string query, int limit = 20)
        {
            var items = new List<QueueItem>();
            try
            {
                var response = await _http.GetAsync($"/v1/catalog/us/search?term={Uri.EscapeDataString(query)}&types=songs&limit={limit}");
                if (!response.IsSuccessStatusCode)
                    return items;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var results = doc.RootElement.GetProperty("results");

                if (results.TryGetProperty("songs", out var songs))
                {
                    var data = songs.GetProperty("data");
                    foreach (var track in data.EnumerateArray())
                    {
                        var attributes = track.GetProperty("attributes");
                        items.Add(ParseQueueItem(track, attributes));
                    }
                }
            }
            catch
            {
            }
            return items;
        }

        public async Task<LyricsStruct> GetLyrics(string trackId)
        {
            try
            {
                var response = await _http.GetAsync($"/v1/catalog/us/songs/{trackId}");
                if (!response.IsSuccessStatusCode)
                    return CreateEmptyLyrics(trackId);

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var data = doc.RootElement.GetProperty("data");
                if (data.GetArrayLength() == 0)
                    return CreateEmptyLyrics(trackId);

                var attributes = data[0].GetProperty("attributes");
                var lines = new List<LyricsLine>();

                if (attributes.TryGetProperty("lyrics", out var lyrics))
                {
                    var lyricsText = lyrics.GetProperty("text").GetString() ?? "";
                    var trackName = attributes.GetProperty("name").GetString() ?? "";
                    var artistName = attributes.GetProperty("artistName").GetString() ?? "";

                    var lineNum = 0;
                    foreach (var textLine in lyricsText.Split('\n'))
                    {
                        var trimmed = textLine.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                        {
                            lines.Add(new LyricsLine { TimeMs = 0, Text = trimmed });
                        }
                        lineNum++;
                    }

                    return new LyricsStruct
                    {
                        TrackId = trackId,
                        TrackName = trackName,
                        Artist = artistName,
                        Lines = lines,
                        IsSynced = false
                    };
                }

                return CreateEmptyLyrics(trackId);
            }
            catch
            {
                return CreateEmptyLyrics(trackId);
            }
        }

        public async Task<bool> LoginWithUserToken(string userToken)
        {
            try
            {
                _http.DefaultRequestHeaders.Add("Media-User-Token", userToken);
                var response = await _http.GetAsync("/v1/me/library/playlists?limit=1");
                if (response.IsSuccessStatusCode)
                    return true;
            }
            catch
            {
            }
            return false;
        }

        private static QueueItem ParseQueueItem(JsonElement track, JsonElement attributes)
        {
            var albumName = attributes.TryGetProperty("albumName", out var album) ? album.GetString() ?? "" : "";
            var artistName = attributes.TryGetProperty("artistName", out var artist) ? artist.GetString() ?? "" : "";
            var artworkUrl = attributes.TryGetProperty("artwork", out var artwork) ? ParseArtworkUrl(artwork) : "";
            var duration = attributes.TryGetProperty("durationInMillis", out var dur) ? dur.GetInt32() : 0;

            return new QueueItem
            {
                Id = track.GetProperty("id").GetString() ?? "",
                Name = attributes.GetProperty("name").GetString() ?? "",
                Artists = new[] { artistName },
                Album = new AlbumStruct { Name = albumName, ImageUrl = artworkUrl },
                DurationMs = duration,
                ImageUrl = artworkUrl
            };
        }

        private static PlaylistItem ParsePlaylistItem(JsonElement track, JsonElement attributes, string playlistId)
        {
            var albumName = attributes.TryGetProperty("albumName", out var album) ? album.GetString() ?? "" : "";
            var artistName = attributes.TryGetProperty("artistName", out var artist) ? artist.GetString() ?? "" : "";
            var artworkUrl = attributes.TryGetProperty("artwork", out var artwork) ? ParseArtworkUrl(artwork) : "";
            var duration = attributes.TryGetProperty("durationInMillis", out var dur) ? dur.GetInt32() : 0;

            return new PlaylistItem
            {
                Id = track.GetProperty("id").GetString() ?? "",
                Name = attributes.GetProperty("name").GetString() ?? "",
                Artists = new[] { artistName },
                Album = new AlbumStruct { Name = albumName, ImageUrl = artworkUrl },
                DurationMs = duration,
                ImageUrl = artworkUrl,
                PlaylistId = playlistId
            };
        }

        private static TrackStruct ParseTrack(JsonElement track, JsonElement attributes)
        {
            var albumName = attributes.TryGetProperty("albumName", out var album) ? album.GetString() ?? "" : "";
            var artistName = attributes.TryGetProperty("artistName", out var artist) ? artist.GetString() ?? "" : "";
            var artworkUrl = attributes.TryGetProperty("artwork", out var artwork) ? ParseArtworkUrl(artwork) : "";
            var duration = attributes.TryGetProperty("durationInMillis", out var dur) ? dur.GetInt32() : 0;

            return new TrackStruct
            {
                Id = track.GetProperty("id").GetString() ?? "",
                Name = attributes.GetProperty("name").GetString() ?? "",
                Artists = new[] { artistName },
                Album = new AlbumStruct { Name = albumName, ImageUrl = artworkUrl },
                DurationMs = duration
            };
        }

        private static string ParseArtworkUrl(JsonElement artwork)
        {
            var url = artwork.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(url))
                return "";
            return url.Replace("{w}", "300").Replace("{h}", "300");
        }

        private static LyricsStruct CreateEmptyLyrics(string trackId)
        {
            return new LyricsStruct
            {
                TrackId = trackId,
                Lines = new List<LyricsLine>(),
                IsSynced = false
            };
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _http.Dispose();
        }
    }

    public class CurrentlyPlayingContext
    {
        public TrackStruct Track;
    }

    public class AppleMusicUser
    {
        public bool IsAuthenticated;
    }
}
