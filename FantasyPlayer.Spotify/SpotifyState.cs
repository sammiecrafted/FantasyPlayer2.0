using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;

namespace FantasyPlayer.Spotify
{
    public class SpotifyState
    {
        //TODO: put this in costs!
        private readonly Uri _loginUrl;
        private readonly string _clientId;
        private readonly int _playerRefreshTime;
        private readonly EmbedIOAuthServer _server;

        private SpotifyClient _spotifyClient;
        private PKCEAuthenticator _authenticator;
        
        private FullTrack _lastFullTrack;
        private PrivateUser _user;
        public bool IsPremiumUser;
        private string _deviceId;

        public PKCETokenResponse TokenResponse;

        public CurrentlyPlayingContext CurrentlyPlaying;
        public delegate void OnPlayerStateUpdateDelegate(CurrentlyPlayingContext currentlyPlaying,
            FullTrack playbackItem);

        public OnPlayerStateUpdateDelegate OnPlayerStateUpdate;

        public delegate void OnLoggedInDelegate(PrivateUser privateUser, PKCETokenResponse tokenResponse);

        public OnLoggedInDelegate OnLoggedIn;

        private readonly ICollection<String> _scopes = new List<string>
        {
            Scopes.UserReadPrivate,
            Scopes.UserReadPlaybackState,
            Scopes.UserModifyPlaybackState,
            Scopes.UserReadCurrentlyPlaying
        };

        private string _challenge;
        private string _verifier;
        private DateTime _challengeCreatedAt;
        private LoginRequest _loginRequest;
        private CancellationTokenSource? _stateUpdateCts;
        private CancellationToken _authToken;

        public string? AuthUri { get; private set; }

        public event Action<string>? OnAuthError;

        public SpotifyState(string loginUri, string clientId, int port, int playerRefreshTime)
        {
            _loginUrl = new Uri(loginUri);
            _clientId = clientId;
            _playerRefreshTime = playerRefreshTime;
            _server = new EmbedIOAuthServer(_loginUrl, port);
            _server.AuthorizationCodeReceived += OnAuthorizationCodeReceived;
            _server.ErrorReceived += (sender, message, context) =>
            {
                OnAuthError?.Invoke($"Callback error: {message}{(string.IsNullOrEmpty(context) ? string.Empty : $" ({context})")}");
                return System.Threading.Tasks.Task.CompletedTask;
            };
        }

        private void GenerateCode()
        {
            (_verifier, _challenge) = PKCEUtil.GenerateCodes();
            _challengeCreatedAt = DateTime.UtcNow;
        }

        private void CreateLoginRequest()
        {
            _loginRequest = new LoginRequest(_loginUrl, _clientId, LoginRequest.ResponseType.Code)
            {
                CodeChallenge = _challenge,
                CodeChallengeMethod = "S256",
                Scope = _scopes
            };
        }

        public async Task RequestToken()
        {
            if (TokenResponse == null)
                return;

            try
            {
                var newResponse = await new OAuthClient().RequestToken(
                    new PKCETokenRefreshRequest(_clientId, TokenResponse.RefreshToken)
                );

                TokenResponse = newResponse;
            }
            catch (Exception e)
            {
                TokenResponse = null;
                OnAuthError?.Invoke($"Token refresh failed, please login again. ({e.Message})");
            }
        }

        public async Task Start(object obj)
        {
            try
            {
                CancellationToken token = (CancellationToken)obj;
                if (token.IsCancellationRequested)
                {
                    _stateUpdateCts?.Cancel();
                    return;
                }
                _authenticator = new PKCEAuthenticator(_clientId!, TokenResponse);

                var config = SpotifyClientConfig.CreateDefault()
                    .WithAuthenticator(_authenticator);

                _spotifyClient = new SpotifyClient(config);

                PrivateUser user = null;
                Exception? lastError = null;
                for (var attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        user = await _spotifyClient.UserProfile.Current();
                        break;
                    }
                    catch (Exception e)
                    {
                        lastError = e;
                        if (attempt < 3)
                        {
                            await Task.Delay(1000 + attempt * 500);
                        }
                    }
                }

                if (user == null)
                {
                    var detail = lastError is APIException apiEx
                        ? $"{apiEx.Response.StatusCode}: {Truncate(apiEx.Response.Body?.ToString(), 200)}"
                        : lastError?.Message;
                    OnAuthError?.Invoke(
                        $"Unable to connect to Spotify after 3 attempts ({lastError?.GetType().Name}: {detail}). Your token is still valid - click \"Retry Connection\" below, or re-login if it persists.");
                    return;
                }


                _user = user;
                //UserPlaylists = playlists;

                if (user.Product == "premium")
                    IsPremiumUser = true;

                OnLoggedIn?.Invoke(_user, TokenResponse);
                _stateUpdateCts = new CancellationTokenSource();
                StateUpdateTimer(_stateUpdateCts.Token);
            }
            catch (Exception e)
            {
                OnAuthError?.Invoke($"Unable to connect to Spotify: {e.Message}");
            }
        }

        public void RetryConnect()
        {
            if (TokenResponse == null)
            {
                OnAuthError?.Invoke("No stored token yet - complete a login first.");
                return;
            }

            Task.Run(() => Start(_authToken));
        }

        private static string Truncate(string? s, int max)
        {
            if (string.IsNullOrEmpty(s))
                return "<no body>";
            return s.Length > max ? s.Substring(0, max) + "..." : s;
        }

        private async Task StateUpdateTimer(object obj)
        {
            CancellationToken token = (CancellationToken)obj;
            while (!token.IsCancellationRequested)
            {
                await CheckPlayerState(token);

                await Task.Delay(_playerRefreshTime, token);
                if (token.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private void UpdatePlayerState(CurrentlyPlayingContext playback, FullTrack playbackItem)
        {
            var lastId = "";

            if (_lastFullTrack != null)
                lastId = _lastFullTrack.Id;

            _deviceId = playback.Device.Id;
            CurrentlyPlaying = playback;
            _lastFullTrack = playbackItem;

            OnPlayerStateUpdate?.Invoke(playback, playbackItem);
        }

        private async Task CheckPlayerState(CancellationToken token)
        {
            try
            {
                var playback = await _spotifyClient.Player.GetCurrentPlayback(token);

                if (playback.Item.Type != ItemType.Track)
                    return; //TODO: Set invalid state

                var playbackItem = (FullTrack) playback.Item;

                if (CurrentlyPlaying == null)
                    UpdatePlayerState(playback, playbackItem);

                if (playbackItem.Id == _lastFullTrack.Id && playback.IsPlaying == CurrentlyPlaying.IsPlaying &&
                    playback.ShuffleState == CurrentlyPlaying.ShuffleState &&
                    playback.RepeatState == CurrentlyPlaying.RepeatState)
                {
                    var inRange = playback.ProgressMs >= CurrentlyPlaying.ProgressMs &&
                                  playback.ProgressMs <= CurrentlyPlaying.ProgressMs + 4500;
                    CurrentlyPlaying.ProgressMs = playback.ProgressMs;
                    if (inRange)
                        return;
                }

                UpdatePlayerState(playback, playbackItem);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public async Task StartAuth(object obj)
        {
            CancellationToken token = (CancellationToken)obj;
            if (token.IsCancellationRequested)
            {
                return;
            }
            GenerateCode();
            CreateLoginRequest();
            AuthUri = _loginRequest.ToUri().ToString();
            _authToken = token;

            try
            {
                await _server.Start();
            }
            catch (Exception e)
            {
                OnAuthError?.Invoke($"Failed to start the local auth server: {e.Message}");
                return;
            }
            if (token.IsCancellationRequested)
            {
                return;
            }
        }

        private async Task OnAuthorizationCodeReceived(object sender, AuthorizationCodeResponse response)
        {
            try
            {
                await _server.Stop();
                TokenResponse = await new OAuthClient().RequestToken(
                    new PKCETokenRequest(_clientId!, response.Code, _server.BaseUri, _verifier)
                );
                Start(_authToken);
            }
            catch (Exception e)
            {
                OnAuthError?.Invoke(
                    $"Spotify rejected the login: {e.Message}. Codes are single-use and expire in ~10 minutes, and each 'Login' starts a fresh one - only use the newest callback URL, without clicking Reset Login in between.");
            }
        }

        private void OpenBrowser(string url)
        {
            try
            {
                _ = Process.Start(new ProcessStartInfo()
                {
                    FileName = url,
                    UseShellExecute = true,
                });
            }
            catch (Exception e)
            {
                OnAuthError?.Invoke(
                    $"Couldn't open the browser automatically. Use the \"Copy Authorize URL\" button instead. ({e.Message})");
            }
        }

        public void RetryLogin()
        {
            if (_loginRequest == null || AuthUri == null)
            {
                OnAuthError?.Invoke("Nothing to re-open yet, start a login first.");
                return;
            }
            OpenBrowser(AuthUri);
        }

        public async Task<bool> CompleteAuthWithCode(string code)
        {
            code = (code ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(code))
            {
                OnAuthError?.Invoke("Please paste the code from the browser's address bar.");
                return false;
            }

            if (code.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                if (!code.Contains('?') || !code.Contains("code=", StringComparison.OrdinalIgnoreCase))
                {
                    if (code.StartsWith("https://accounts.spotify.com/authorize",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        OnAuthError?.Invoke(
                            "That's the Spotify authorization page, not a login code. Opening it in your browser now - after you click Agree, paste the \"127.0.0.1:2984/callback?code=...\" address you land on into this box.");
                        RetryLogin();
                    }
                    else
                    {
                        OnAuthError?.Invoke(
                            "No \"code\" parameter was found in that URL. Paste the full callback address, e.g. http://127.0.0.1:2984/callback?code=...");
                    }

                    return false;
                }

                code = ExtractCodeFromCallbackUrl(code);
            }

            if (string.IsNullOrEmpty(code))
            {
                OnAuthError?.Invoke("No \"code\" parameter was found in that URL.");
                return false;
            }

            try
            {
                try
                {
                    await _server.Stop();
                }
                catch (Exception)
                {
                    // The server may not have been started, ignore.
                }
                TokenResponse = await new OAuthClient().RequestToken(
                    new PKCETokenRequest(_clientId!, code, _server.BaseUri, _verifier)
                );
                Start(_authToken);
                return true;
            }
            catch (Exception e)
            {
                var ageSeconds = (int)(DateTime.UtcNow - _challengeCreatedAt).TotalSeconds;
                OnAuthError?.Invoke(
                    $"Spotify rejected the login: {e.Message}. This code's authorization challenge was created {ageSeconds}s ago. Codes are single-use and expire in ~10 minutes, and each 'Login' starts a fresh one - only use the newest callback URL, without clicking Reset Login in between.");
                return false;
            }
        }

        private string ExtractCodeFromCallbackUrl(string url)
        {
            var query = url.Contains('?') ? url.Substring(url.IndexOf('?') + 1) : string.Empty;
            foreach (var pair in query.Split('&'))
            {
                var parts = pair.Split('=');
                if (parts.Length == 2 && parts[0] == "code")
                {
                    return Uri.UnescapeDataString(parts[1]);
                }
            }
            return string.Empty;
        }

        public void PauseOrPlay(bool play)
        {
            try
            {
                if (CurrentlyPlaying == null) return;
                if (play)
                    _spotifyClient.Player.ResumePlayback(new PlayerResumePlaybackRequest {DeviceId = _deviceId});

                if (!play)
                    _spotifyClient.Player.PausePlayback(new PlayerPausePlaybackRequest {DeviceId = _deviceId});
            }
            catch (APIException)
            {
            }
        }

        public async Task Shuffle(bool value)
        {
            try
            {
                if (CurrentlyPlaying == null) return;
                //CurrentlyPlaying.ShuffleState = !CurrentlyPlaying.ShuffleState;
                var shuffle = new PlayerShuffleRequest(value) {DeviceId = _deviceId};
                await _spotifyClient.Player.SetShuffle(shuffle);
            }
            catch (APIException)
            {
            }
        }

        public async Task SwapRepeatState()
        {
            var state = CurrentlyPlaying.RepeatState switch
            {
                "off" => PlayerSetRepeatRequest.State.Context,
                "context" => PlayerSetRepeatRequest.State.Track,
                "track" => PlayerSetRepeatRequest.State.Off,
                _ => PlayerSetRepeatRequest.State.Off
            };

            try
            {
                if (CurrentlyPlaying == null) return;
                //CurrentlyPlaying.RepeatState = state.ToString().ToLower();
                var repeat = new PlayerSetRepeatRequest(state) {DeviceId = _deviceId};
                await _spotifyClient.Player.SetRepeat(repeat);
            }
            catch (APIException)
            {
            }
        }

        public void Skip(bool forward)
        {
            try
            {
                if (CurrentlyPlaying == null) return;
                if (forward)
                    _spotifyClient.Player.SkipNext(new PlayerSkipNextRequest {DeviceId = _deviceId});

                if (!forward)
                    _spotifyClient.Player.SkipPrevious(new PlayerSkipPreviousRequest {DeviceId = _deviceId});
            }
            catch (APIException)
            {
            }
        }

        public async void SetVolume(int volume)
        {
            try
            {
                if (volume > 100 || volume < 0) return;
                var request = new PlayerVolumeRequest(volume) {DeviceId = _deviceId};
                await _spotifyClient.Player.SetVolume(request);
            }
            catch (APIException)
            {
            }
        }

        public void Reset()
        {
            _stateUpdateCts?.Cancel();
            _stateUpdateCts?.Dispose();
            _stateUpdateCts = null;
            try
            {
                _server?.Stop();
            }
            catch (Exception)
            {
                // The server may not be running, ignore.
            }

            _spotifyClient = null;
            _authenticator = null;
            _user = null;
            _lastFullTrack = null;
            CurrentlyPlaying = null;
            TokenResponse = null;
            _loginRequest = null;
            AuthUri = null;
            IsPremiumUser = false;
        }

        public void Dispose()
        {
            _stateUpdateCts?.Cancel();
            _stateUpdateCts?.Dispose();
            _server?.Stop();
            _spotifyClient = null;
        }
    }
}