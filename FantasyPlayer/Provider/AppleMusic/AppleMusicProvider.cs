using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FantasyPlayer.Config;
using FantasyPlayer.Interface;
using FantasyPlayer.Interfaces;
using FantasyPlayer.Manager;
using FantasyPlayer.Provider.AppleMusic;
using FantasyPlayer.Provider.Common;

namespace FantasyPlayer.Provider.AppleMusic
{
    using Dalamud.Plugin.Services;

    public class AppleMusicProvider : IPlayerProvider
    {
        private readonly Configuration configuration;
        private readonly ChatMessageService chatMessageService;
        private readonly IChatGui chatGui;

        private AppleMusicClient? _client;
        private bool initialized;
        private bool disposed;
        private string? _lastError;
        private string _lastId = string.Empty;
        private DateTime _lastPoll = DateTime.MinValue;
        private bool _polling;
        private int _volume = 50;

        public AppleMusicProvider(Configuration configuration, ChatMessageService chatMessageService, IChatGui chatGui)
        {
            this.configuration = configuration;
            this.chatMessageService = chatMessageService;
            this.chatGui = chatGui;
        }

        public PlayerStateStruct PlayerState { get; set; } = new PlayerStateStruct
        {
            ServiceName = "Apple Music",
            RequiresLogin = true
        };

        public string Key => "apple";

        public string Name => "Apple Music";

        public bool Initialized => initialized;

        public string? AuthUri => null;

        public string? LastAuthError => _lastError;

        public async Task<IPlayerProvider> Initialize()
        {
            PlayerState = new PlayerStateStruct
            {
                ServiceName = "Apple Music",
                RequiresLogin = true
            };

            var settings = configuration.AppleMusicSettings;
            if (string.IsNullOrEmpty(settings.DeveloperToken))
            {
                _lastError = "No Apple Music Developer Token configured. Set one in the settings.";
                initialized = true;
                return this;
            }

            _client = new AppleMusicClient(settings.DeveloperToken, settings.MusicUserToken);

            if (!string.IsNullOrEmpty(settings.MusicUserToken))
            {
                var user = await _client.GetUserProfile();
                if (user != null && user.IsAuthenticated)
                {
                    var state = PlayerState;
                    state.IsLoggedIn = true;
                    PlayerState = state;
                }
            }

            initialized = true;
            return this;
        }

        public void Update()
        {
            if (!initialized || _client == null || disposed)
                return;

            if ((DateTime.Now - _lastPoll).TotalMilliseconds < 3000 || _polling)
                return;

            _polling = true;
            _ = Task.Run(PollAsync);
        }

        private async Task PollAsync()
        {
            try
            {
                if (_client == null) return;

                var context = await _client.GetCurrentPlayback();
                if (context == null) return;

                var state = PlayerState;
                var track = context.Track;

                state.IsPlaying = true;
                state.Volume = _volume;
                state.CurrentlyPlaying = track;

                state.HasQueueSupport = true;
                state.HasPlaylistSupport = true;
                state.HasLyricsSupport = true;

                if (track.Id != _lastId)
                {
                    _lastId = track.Id ?? string.Empty;
                    chatMessageService.DisplaySongTitle(track.Name);
                }

                if (!state.IsLoggedIn)
                    state.IsLoggedIn = true;

                PlayerState = state;
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                _polling = false;
                _lastPoll = DateTime.Now;
            }
        }

        public void SwapRepeatState()
        {
            var state = PlayerState;
            var current = state.RepeatState;
            state.RepeatState = current switch
            {
                "off" => "context",
                "context" => "track",
                _ => "off"
            };
            PlayerState = state;
        }

        public void SetPauseOrPlay(bool play)
        {
            chatGui.Print("Apple Music: Play/Pause control requires native MusicKit integration (not yet implemented).");
        }

        public void SetSkip(bool forward)
        {
            chatGui.Print("Apple Music: Skip control requires native MusicKit integration (not yet implemented).");
        }

        public void SetShuffle(bool value)
        {
            chatGui.Print("Apple Music: Shuffle control requires native MusicKit integration (not yet implemented).");
        }

        public void SetVolume(int volume)
        {
            _volume = Math.Clamp(volume, 0, 100);
            var state = PlayerState;
            state.Volume = _volume;
            PlayerState = state;
        }

        public void Seek(int positionMs)
        {
        }

        public void AddToQueue(string trackId)
        {
        }

        public async Task<List<QueueItem>> GetQueue()
        {
            if (_client == null) return new List<QueueItem>();
            return await _client.GetQueue();
        }

        public void RemoveFromQueue(int index)
        {
        }

        public async Task<List<PlaylistStruct>> GetPlaylists()
        {
            if (_client == null) return new List<PlaylistStruct>();
            return await _client.GetPlaylists();
        }

        public async Task<PlaylistTrackList> GetPlaylistTracks(string playlistId)
        {
            if (_client == null) return new PlaylistTrackList { Tracks = new List<PlaylistItem>() };
            return await _client.GetPlaylistTracks(playlistId);
        }

        public async Task<PlaylistTrackList> SearchPlaylists(string query)
        {
            return new PlaylistTrackList();
        }

        public void PlayPlaylist(string playlistId, int trackIndex = 0)
        {
            chatGui.Print("Apple Music: Playlist playback requires native MusicKit integration (not yet implemented).");
        }

        public async Task<List<QueueItem>> SearchTracks(string query)
        {
            if (_client == null) return new List<QueueItem>();
            return await _client.SearchTracks(query);
        }

        public async Task<LyricsStruct> GetLyrics()
        {
            if (_client == null || string.IsNullOrEmpty(PlayerState.CurrentlyPlaying.Id))
                return new LyricsStruct { Lines = new List<LyricsLine>() };

            return await _client.GetLyrics(PlayerState.CurrentlyPlaying.Id);
        }

        private readonly Dictionary<string, int> _playerVolumes = new();

        public void SetPlayerVolume(string playerName, int volume)
        {
            _playerVolumes[playerName] = Math.Clamp(volume, 0, 100);
            SetVolume(volume);
        }

        public int GetPlayerVolume(string playerName)
        {
            return _playerVolumes.TryGetValue(playerName, out var vol) ? vol : PlayerState.Volume;
        }

        public void StartAuth()
        {
        }

        public void RetryAuth()
        {
            _ = Reconnect();
        }

        public void ReAuth()
        {
        }

        public void CompleteAuth(string code)
        {
        }

        public void ClearAuthError()
        {
            _lastError = null;
        }

        public async Task<bool> Reconnect()
        {
            if (_client == null) return false;

            var settings = configuration.AppleMusicSettings;
            if (string.IsNullOrEmpty(settings.MusicUserToken))
                return false;

            var ok = await _client.LoginWithUserToken(settings.MusicUserToken);
            var state = PlayerState;
            state.IsLoggedIn = ok;
            if (ok)
            {
                settings.IsLoggedIn = true;
                state.IsAuthenticating = false;
            }
            PlayerState = state;
            return ok;
        }

        public void ResetLogin()
        {
            configuration.AppleMusicSettings.ClearLogin();
            _lastError = null;
            var state = PlayerState;
            state.IsLoggedIn = false;
            PlayerState = state;
        }

        public void RetryConnect()
        {
            _ = Reconnect();
        }

        public void Dispose()
        {
            disposed = true;
            _client?.Dispose();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Initialize();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
