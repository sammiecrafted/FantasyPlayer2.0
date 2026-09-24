using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FantasyPlayer.Config;
using FantasyPlayer.Interface;
using FantasyPlayer.Interfaces;
using FantasyPlayer.Manager;
using FantasyPlayer.Provider.Common;

namespace FantasyPlayer.Provider.Local
{
    using Dalamud.Plugin.Services;

    /// <summary>
    /// Free music provider backed by a local MPD (Music Player Daemon).
    /// Plays internet radio stations and music files/folders without any subscription.
    /// </summary>
    public class LocalProvider : IPlayerProvider
    {
        private readonly Configuration configuration;
        private readonly ChatMessageService chatMessageService;
        private readonly IChatGui chatGui;

        private MpdClient? _client;
        private RadioBrowserService? _radioBrowser;
        private bool initialized;
        private DateTime _lastPoll = DateTime.MinValue;
        private DateTime _lastConnectAttempt = DateTime.MinValue;
        private bool _polling;
        private bool _disposed;
        private string? _lastError;
        private string _lastId = string.Empty;

        public LocalProvider(Configuration configuration, ChatMessageService chatMessageService, IChatGui chatGui)
        {
            this.configuration = configuration;
            this.chatMessageService = chatMessageService;
            this.chatGui = chatGui;
        }

        public PlayerStateStruct PlayerState { get; set; } = new PlayerStateStruct
        {
            ServiceName = "Local",
            RequiresLogin = false
        };

        public string Key => "local";

        public string Name => "Local";

        public RadioBrowserService RadioBrowser => _radioBrowser ??= new RadioBrowserService();

        public bool Initialized => initialized;

        public string? AuthUri => null;

        public string? LastAuthError => _lastError;

        public async Task<IPlayerProvider> Initialize()
        {
            PlayerState = new PlayerStateStruct
            {
                ServiceName = "Local",
                RequiresLogin = false
            };

            _client = new MpdClient(configuration.LocalSettings.Host, configuration.LocalSettings.Port, configuration.LocalSettings.Password);
            await Connect();
            if (_client.IsConnected)
            {
                var state = PlayerState;
                state.IsLoggedIn = true;
                PlayerState = state;
            }

            initialized = true;
            return this;
        }

        private async Task Connect()
        {
            if (_client == null)
            {
                return;
            }

            _lastConnectAttempt = DateTime.Now;
            var ok = await _client.ConnectAsync();
            if (!ok && _client.LastError != null)
            {
                _lastError = _client.LastError;
            }

            var state = PlayerState;
            state.IsLoggedIn = ok;
            if (ok)
            {
                state.IsAuthenticating = false;
                state.IsLoggedIn = true;
            }

            PlayerState = state;
        }

        public void Update()
        {
            if (!initialized || _client == null || _disposed)
            {
                return;
            }

            if (!_client.IsConnected && (DateTime.Now - _lastConnectAttempt).TotalSeconds < 5)
            {
                return;
            }

            if ((DateTime.Now - _lastPoll).TotalMilliseconds < 1500 || _polling)
            {
                return;
            }

            _polling = true;
            _ = Task.Run(PollAsync);
        }

        private async Task PollAsync()
        {
            try
            {
                try
                {
                    if (!_client!.IsConnected)
                    {
                        await Connect();
                    }

                    if (_client.IsConnected)
                    {
                        await RefreshState();
                    }
                }
                catch (System.ObjectDisposedException)
                {
                    // Provider is being unloaded; a poll was already in flight. Safe to ignore.
                }
            }
            finally
            {
                _polling = false;
            }
        }

        private async Task RefreshState()
        {
            var status = await _client!.StatusAsync();
            if (!_client.IsConnected)
            {
                return;
            }

            var song = await _client.CurrentSongAsync();
            var state = PlayerState;

            state.ProgressMs = ParseInt(status, "elapsed") * 1000;
            state.IsPlaying = status.GetValueOrDefault("state") == "play";
            state.RepeatState = MapRepeat(status);
            state.ShuffleState = status.GetValueOrDefault("random") == "1";
            state.Volume = ParseInt(status, "volume");

            if (song.Count == 0)
            {
                state.CurrentlyPlaying = new TrackStruct();
                PlayerState = state;
                return;
            }

            var id = song.GetValueOrDefault("songid") ?? song.GetValueOrDefault("file");
            state.CurrentlyPlaying = new TrackStruct
            {
                Id = id,
                Name = song.GetValueOrDefault("Title") ?? FallbackName(song.GetValueOrDefault("file")),
                Artists = new[] { song.GetValueOrDefault("Artist") ?? "Unknown" },
                Album = new AlbumStruct
                {
                    Name = song.GetValueOrDefault("Album") ?? string.Empty
                },
                DurationMs = GetDuration(song)
            };

            if (id != _lastId)
            {
                _lastId = id ?? string.Empty;
                chatMessageService.DisplaySongTitle(state.CurrentlyPlaying.Name);
            }

            if (!state.IsLoggedIn)
            {
                state.IsLoggedIn = true;
            }

            PlayerState = state;
        }

        private static string MapRepeat(Dictionary<string, string> status)
        {
            var repeat = status.GetValueOrDefault("repeat") == "1";
            var single = status.GetValueOrDefault("single") == "1";
            if (repeat && single)
            {
                return "track";
            }

            return repeat ? "context" : "off";
        }

        private static int GetDuration(Dictionary<string, string> song)
        {
            var time = song.GetValueOrDefault("Time");
            if (time != null)
            {
                var parts = time.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1], out var total) && total > 0)
                {
                    return total * 1000;
                }
            }

            if (int.TryParse(song.GetValueOrDefault("duration"), out var duration) && duration > 0)
            {
                return duration * 1000;
            }

            return 6 * 60 * 60 * 1000;
        }

        private static string FallbackName(string? file)
        {
            if (string.IsNullOrEmpty(file))
            {
                return "Unknown";
            }

            var idx = file.LastIndexOf('/');
            if (idx >= 0 && idx < file.Length - 1)
            {
                file = file.Substring(idx + 1);
            }

            var dot = file.LastIndexOf('.');
            return dot > 0 ? file.Substring(0, dot) : file;
        }

        private static int ParseInt(Dictionary<string, string> dict, string key)
        {
            return int.TryParse(dict.GetValueOrDefault(key), out var value) ? value : 0;
        }

        private async Task Run(string command, string errorPrefix)
        {
            if (_client == null)
            {
                return;
            }

            if (!_client.IsConnected && !await Reconnect())
            {
                return;
            }

            await _client.CommandAsync(command);
            if (!_client.IsConnected)
            {
                _lastError = $"{errorPrefix}: MPD disconnected while sending '{command}'.";
                chatGui.PrintError(_lastError!);
            }

            if (_client.LastError != null)
            {
                _lastError = $"{errorPrefix}: {_client.LastError}";
                chatGui.PrintError(_lastError);
            }
        }

        public void PlayStation(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                chatGui.PrintError("Local: no station URL selected.");
                return;
            }

            _ = Task.Run(async () =>
            {
                await Run("clear", "Local");
                await Run($"add \"{url.Replace("\"", "\\\"")}\"", "Local");
                await Run("play", "Local");
            });
        }

        public void PlayFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                chatGui.PrintError("Local: no music folder configured. Set one in the settings.");
                return;
            }

            _ = Task.Run(async () =>
            {
                await Run("clear", "Local");
                await Run($"add \"{folder.Replace("\"", "\\\"")}\"", "Local");
                await Run("repeat 1", "Local");
                await Run("single 1", "Local");
                await Run("play", "Local");
            });
        }

        public async Task<bool> Reconnect()
        {
            if (_client == null)
            {
                return false;
            }

            await Connect();
            if (!_client.IsConnected)
            {
                _lastError = $"Could not connect to MPD at {configuration.LocalSettings.Host}:{configuration.LocalSettings.Port}. Ensure mpd is installed and running (see SETUP.md).{(_client.LastError != null ? " " + _client.LastError : string.Empty)}";
                chatGui.PrintError($"Local: {_lastError}");
            }

            return _client.IsConnected;
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

        public void RetryConnect()
        {
            _ = Reconnect();
        }

        public void ResetLogin()
        {
            _ = Run("stop", "Local");
            _lastError = null;
        }

        public void SwapRepeatState()
        {
            var current = PlayerState.RepeatState;
            switch (current)
            {
                case "off":
                    _ = Run("repeat 1", "Local");
                    _ = Run("single 0", "Local");
                    break;
                case "context":
                    _ = Run("repeat 1", "Local");
                    _ = Run("single 1", "Local");
                    break;
                default:
                    _ = Run("repeat 0", "Local");
                    _ = Run("single 0", "Local");
                    break;
            }
        }

        public void SetPauseOrPlay(bool play)
        {
            _ = Run(play ? "play" : "pause 1", "Local");
        }

        public void SetSkip(bool forward)
        {
            _ = Run(forward ? "next" : "previous", "Local");
        }

        public void SetShuffle(bool value)
        {
            _ = Run(value ? "random 1" : "random 0", "Local");
        }

        public void SetVolume(int volume)
        {
            _ = Run($"setvol {Math.Clamp(volume, 0, 100)}", "Local");
        }

        public void Dispose()
        {
            _disposed = true;
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