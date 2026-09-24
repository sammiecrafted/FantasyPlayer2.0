using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FantasyPlayer.Interface;
using FantasyPlayer.Interfaces;
using FantasyPlayer.Provider.Common;
using FantasyPlayer.Spotify;
using SpotifyAPI.Web;

namespace FantasyPlayer.Provider
{
    using Config;
    using Dalamud.Plugin.Services;
    using Manager;

    public class SpotifyProvider : IPlayerProvider
    {
        private readonly ConfigurationManager configurationManager;
        private readonly Configuration configuration;
        private readonly ChatMessageService chatMessageService;
        private readonly IChatGui chatGui;

        public SpotifyProvider(ConfigurationManager configurationManager, Configuration configuration, ChatMessageService chatMessageService, IChatGui chatGui)
        {
            this.configurationManager = configurationManager;
            this.configuration = configuration;
            this.chatMessageService = chatMessageService;
            this.chatGui = chatGui;
        }

        public PlayerStateStruct PlayerState { get; set; }

        private SpotifyState? _spotifyState;
        private string? _lastAuthError;
        private string _lastId;

        private CancellationTokenSource _startCts;
        private CancellationTokenSource _loginCts;
        private bool initialized;

        public async Task<IPlayerProvider> Initialize()
        {
            PlayerState = new PlayerStateStruct
            {
                ServiceName = "Spotify",
                RequiresLogin = true
            };

            if (string.IsNullOrEmpty(configuration.SpotifySettings.SpotifyClientId))
            {
                initialized = true;
                return this;
            }

            _spotifyState = new SpotifyState(Constants.SpotifyLoginUri, configuration.SpotifySettings.SpotifyClientId, Constants.SpotifyLoginPort, Constants.SpotifyPlayerRefreshTime);

            _spotifyState.OnLoggedIn += OnLoggedIn;
            _spotifyState.OnPlayerStateUpdate += OnPlayerStateUpdate;
            _spotifyState.OnAuthError += OnAuthError;

            if (configuration.SpotifySettings.TokenResponse == null)
            {
                initialized = true;
                return this;
            }
            _spotifyState.TokenResponse = configuration.SpotifySettings.TokenResponse;
            await _spotifyState.RequestToken();
            if (_spotifyState.TokenResponse == null)
            {
                return this;
            }
            _startCts = new CancellationTokenSource();
            await Task.Run(() => _spotifyState.Start(_startCts.Token), _startCts.Token);
            initialized = true;
            return this;
        }

        public string Key => "spotify";

        public string Name => "Spotify";

        public bool Initialized => initialized;

        public string? AuthUri => _spotifyState?.AuthUri;

        public string? LastAuthError => _lastAuthError;

        private void OnAuthError(string message)
        {
            _lastAuthError = message;
            chatGui.PrintError(message);
        }

        private void OnPlayerStateUpdate(CurrentlyPlayingContext currentlyPlaying, FullTrack playbackItem)
        {
            if (playbackItem.Id != _lastId)
                chatMessageService.DisplaySongTitle(playbackItem.Name);
            _lastId = playbackItem.Id;


            var playerStateStruct = PlayerState;
            playerStateStruct.ProgressMs = currentlyPlaying.ProgressMs;
            playerStateStruct.IsPlaying = currentlyPlaying.IsPlaying;
            playerStateStruct.RepeatState = currentlyPlaying.RepeatState;
            playerStateStruct.ShuffleState = currentlyPlaying.ShuffleState;

            playerStateStruct.CurrentlyPlaying = new TrackStruct
            {
                Id = playbackItem.Id,
                Name = playbackItem.Name,
                Artists = playbackItem.Artists.Select(artist => artist.Name).ToArray(),
                DurationMs = playbackItem.DurationMs,
                Album = new AlbumStruct
                {
                    Name = playbackItem.Album.Name
                }
            };

            PlayerState = playerStateStruct;
        }

        private void OnLoggedIn(PrivateUser privateUser, PKCETokenResponse tokenResponse)
        {
            var playerStateStruct = PlayerState;
            playerStateStruct.IsLoggedIn = true;
            PlayerState = playerStateStruct;

            configuration.SpotifySettings.TokenResponse = tokenResponse;

            if (_spotifyState!.IsPremiumUser)
                configuration.SpotifySettings.LimitedAccess = false;

            if (!_spotifyState.IsPremiumUser)
            {
                if (!configuration.SpotifySettings.LimitedAccess
                ) //Do a check to not spam the user, I don't want to force it down their throats. (fuck marketing)
                    chatGui.PrintError(
                        "Uh-oh, it looks like you're not premium on Spotify. Some features in Fantasy Player have been disabled.");

                configuration.SpotifySettings.LimitedAccess = true;

                //Change configs
                if (configuration.PlayerSettings.CompactPlayer)
                    configuration.PlayerSettings.CompactPlayer = false;
                if (!configuration.PlayerSettings.NoButtons)
                    configuration.PlayerSettings.NoButtons = true;
            }
        }

        public void Update()
        {
        }

        public void ReAuth()
        {
            //StartAuth();
        }

        public void Dispose()
        {
            if (_startCts != null)
            {
                _startCts.Cancel();
                _startCts.Dispose();
            }

            if (_loginCts != null)
            {
                _loginCts.Cancel();
                _loginCts.Dispose();
            }

            if (_spotifyState != null)
            {
                _spotifyState.OnLoggedIn -= OnLoggedIn;
                _spotifyState.OnPlayerStateUpdate -= OnPlayerStateUpdate;
                _spotifyState.OnAuthError -= OnAuthError;
                _spotifyState.Dispose();
            }
        }

        public void StartAuth()
        {
            _lastAuthError = null;
            if (_spotifyState == null)
            {
                OnAuthError("No Spotify Client ID configured. Please add one in the settings.");
                return;
            }
            _loginCts = new CancellationTokenSource();
            Task.Run(() => _spotifyState.StartAuth(_loginCts.Token), _loginCts.Token);
            var playerStateStruct = PlayerState;
            playerStateStruct.IsAuthenticating = true;
            PlayerState = playerStateStruct;
        }

        public void RetryAuth()
        {
            if (_spotifyState == null)
            {
                OnAuthError("No Spotify Client ID configured. Please add one in the settings.");
                return;
            }
            _spotifyState.RetryLogin();
        }

        public void CompleteAuth(string code)
        {
            if (_spotifyState == null)
            {
                OnAuthError("No Spotify Client ID configured. Please add one in the settings.");
                return;
            }
            _ = Task.Run(() => _spotifyState.CompleteAuthWithCode(code));
        }

        public void ClearAuthError()
        {
            _lastAuthError = null;
        }

        public void RetryConnect()
        {
            _spotifyState?.RetryConnect();
        }

        public void ResetLogin()
        {
            _loginCts?.Cancel();
            _loginCts?.Dispose();
            _loginCts = null;
            _startCts?.Cancel();
            _startCts?.Dispose();
            _startCts = null;
            _lastAuthError = null;
            _spotifyState?.Reset();
            configuration.SpotifySettings.TokenResponse = null;
            var playerStateStruct = PlayerState;
            playerStateStruct.IsAuthenticating = false;
            playerStateStruct.IsLoggedIn = false;
            PlayerState = playerStateStruct;
        }

        public void SwapRepeatState()
        {
            if (_spotifyState!.CurrentlyPlaying != null)
                _spotifyState.SwapRepeatState();
        }

        public void SetPauseOrPlay(bool play)
        {
            if (_spotifyState!.CurrentlyPlaying != null)
                _spotifyState.PauseOrPlay(play);
        }

        public void SetSkip(bool forward)
        {
            if (_spotifyState!.CurrentlyPlaying != null)
                _spotifyState.Skip(forward);
        }

        public void SetShuffle(bool value)
        {
            if (_spotifyState!.CurrentlyPlaying != null)
                _spotifyState.Shuffle(value);
        }

        public void SetVolume(int volume)
        {
            if (_spotifyState!.CurrentlyPlaying != null)
                _spotifyState.SetVolume(volume);
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