using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using FantasyPlayer.Config;
using FantasyPlayer.Interfaces;
using FantasyPlayer.Manager;
using FantasyPlayer.Provider;
using FantasyPlayer.Provider.Common;
using FantasyPlayer.Provider.Local;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Microsoft.Extensions.Logging;
using OtterGui;

namespace FantasyPlayer.Interface.Window
{
    using DalaMock.Host.Mediator;
    using DalaMock.Shared.Interfaces;
    using Dalamud.Interface.Colors;
    using Dalamud.Interface.Windowing;
    using Dalamud.Plugin.Services;
    using FantasyPlayer.Mediator;
    using Serilog;

    public class PlayerWindow : UpdatingWindow
    {
        private readonly IUiBuilder uiBuilder;
        private readonly IFont font;
        private readonly PlayerManager _playerManager;
        private readonly Configuration configuration;
        private readonly ICondition condition;
        private readonly IPlayerState _playerState;
        private readonly SpotifyLoginWindow spotifyLoginWindow;

        private DateTime? _lastUpdated;
        private DateTime? _lastPaused;
        private TimeSpan _difference;
        private int _progressMs;
        private string _lastId;
        private bool _lastBoundByDuty;
        private int _localStationIndex;
        private string _radioQuery = "";
        private List<Config.RadioStation> _radioResults = new List<Config.RadioStation>();
        private bool _radioBusy;

        private readonly Vector2 _playerWindowSize = new Vector2(401 * ImGui.GetIO().FontGlobalScale,
            89 * ImGui.GetIO().FontGlobalScale);

        private readonly Vector2 _windowSizeNoButtons = new Vector2(401 * ImGui.GetIO().FontGlobalScale,
            62 * ImGui.GetIO().FontGlobalScale);

        private readonly Vector2 _windowSizeCompact = new Vector2(179 * ImGui.GetIO().FontGlobalScale,
            39 * ImGui.GetIO().FontGlobalScale);


        public PlayerWindow(ILogger<PlayerWindow> logger, IUiBuilder uiBuilder, IFont font, MediatorService mediatorService, PlayerManager playerManager, Configuration configuration, ICondition condition, IPlayerState playerState, SpotifyLoginWindow spotifyLoginWindow) : base(logger, mediatorService, "Fantasy Player - Player")
        {
            this.uiBuilder = uiBuilder;
            this.font = font;
            _playerManager = playerManager;
            this.configuration = configuration;
            this.condition = condition;
            _playerState = playerState;
            this.spotifyLoginWindow = spotifyLoginWindow;
            SetDefaultWindowSize();
            MediatorService.Subscribe<ConfigurationUpdatedMessage>(this, ConfigurationUpdated );
            this.uiBuilder.OpenMainUi += UiBuilderOnOpenMainUi;
        }

        private void UiBuilderOnOpenMainUi()
        {
            configuration.PlayerSettings.PlayerWindowShown = true;
        }

        private void ConfigurationUpdated(ConfigurationUpdatedMessage obj)
        {
            if (configuration.PlayerSettings.PlayerWindowShown && !IsOpen)
            {
                IsOpen = true;
            }
            else if(!configuration.PlayerSettings.PlayerWindowShown && IsOpen)
            {
                IsOpen = false;
            }

            var lockFlags = (configuration.PlayerSettings.PlayerLocked)
                ? ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize
                : ImGuiWindowFlags.None;

            var clickThroughFlags = (configuration.PlayerSettings.DisableInput)
                ? ImGuiWindowFlags.NoMouseInputs | ImGuiWindowFlags.NoResize
                : ImGuiWindowFlags.None;

            var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | lockFlags |
                        clickThroughFlags;
            if (Flags != flags)
            {
                Flags = flags;
            }

            SetDefaultWindowSize();
        }

        public override void OnClose()
        {
            configuration.PlayerSettings.PlayerWindowShown = false;
            base.OnClose();
        }

        public override bool DrawConditions()
        {
            if (configuration.PlayerSettings.OnlyOpenWhenLoggedIn &&
                _playerState.ContentId == 0)
            {
                return false;
            }

            return base.DrawConditions();
        }

        public override void Draw()
        {
            if (configuration.PlayerSettings.OnlyOpenWhenLoggedIn &&
                _playerState.ContentId == 0)
            {
            }
            else if (_playerManager.CurrentPlayerProvider == null &&
                     configuration.PlayerSettings.PlayerWindowShown)
            {
                DrawWelcome();
            }
            else if (_playerManager.CurrentPlayerProvider != null &&
                     _playerManager.CurrentPlayerProvider.Initialized &&
                     _playerManager.CurrentPlayerProvider.PlayerState.RequiresLogin &&
                     configuration.PlayerSettings.PlayerWindowShown &&
                     !_playerManager.CurrentPlayerProvider.PlayerState.IsLoggedIn)
            {
                DrawLoginPrompt();
            }
            else if (_playerManager.CurrentPlayerProvider != null &&
                     _playerManager.CurrentPlayerProvider is LocalProvider localProvider &&
                     localProvider.Initialized &&
                     configuration.PlayerSettings.PlayerWindowShown)
            {
                DrawMain(_playerManager.CurrentPlayerProvider.PlayerState, _playerManager.CurrentPlayerProvider);
            }
            else if (_playerManager.CurrentPlayerProvider != null &&
                     _playerManager.CurrentPlayerProvider.Initialized &&
                     _playerManager.CurrentPlayerProvider.PlayerState.IsLoggedIn &&
                     configuration.PlayerSettings.PlayerWindowShown)
            {
                DrawMain(_playerManager.CurrentPlayerProvider.PlayerState, _playerManager.CurrentPlayerProvider);
                CheckClientState();
            }
            else
            {
                ImGui.Text("Loading, please wait...");
            }
        }

        private void CheckClientState()
        {
            if (_playerManager.CurrentPlayerProvider == null)
            {
                return;
            }

            var isBoundByDuty = condition[ConditionFlag.BoundByDuty];
            if (configuration.AutoPlaySettings.PlayInDuty && isBoundByDuty &&
                !_playerManager.CurrentPlayerProvider.PlayerState.IsPlaying)
            {
                if (_lastBoundByDuty == false)
                {
                    _lastBoundByDuty = true;
                    _playerManager.CurrentPlayerProvider.SetPauseOrPlay(true);
                }
            }

            _lastBoundByDuty = isBoundByDuty;
        }

        public void DrawWelcome()
        {
            if (string.IsNullOrEmpty(configuration.SpotifySettings.SpotifyClientId))
            {
                InterfaceUtils.TextCentered($"Due to Spotify API changes, you will need to configure a spotify client ID.");
                InterfaceUtils.TextCentered($"Please open the settings to get instructions.");
                if (InterfaceUtils.ButtonCentered("Open Settings"))
                {
                    configuration.ConfigShown = true;
                }
                return;
            }
            else if (_playerManager.ProvidersLoading)
            {
                InterfaceUtils.TextCentered($"The music providers are still being loaded.");
                return;
            }

            InterfaceUtils.TextCentered("Please select your default provider.");
            foreach (var provider in _playerManager.PlayerProviders)
            {
                ImGui.SameLine();
                if (ImGui.Button(provider.Name.Replace("Provider", "")))
                {
                    _playerManager.CurrentPlayerProvider = provider;
                    configuration.PlayerSettings.DefaultProvider = provider.Key;
                }
            }
        }

        public void DrawLoginPrompt()
        {
            var playerProvider = _playerManager.CurrentPlayerProvider;
            if (playerProvider == null || !playerProvider.Initialized)
            {
                return;
            }

            if (!string.IsNullOrEmpty(playerProvider.LastAuthError))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
                InterfaceUtils.TextCentered(pageAwareText(playerProvider.LastAuthError!));
                ImGui.PopStyleColor();
                if (InterfaceUtils.ButtonCentered("Retry Connection"))
                {
                    playerProvider.ClearAuthError();
                    playerProvider.RetryConnect();
                }
            }

            InterfaceUtils.TextCentered($"Spotify is not connected.");
            if (InterfaceUtils.ButtonCentered("Connect Spotify Account"))
            {
                spotifyLoginWindow.Open();
            }

            if (InterfaceUtils.ButtonCentered("Reset Login"))
            {
                playerProvider.ResetLogin();
            }
        }

        private string pageAwareText(string input) => input.Length > 400 ? input.Substring(0, 400) + "..." : input;

        public override void Update()
        {


        }

        private void SetDefaultWindowSize()
        {
            if (configuration.PlayerSettings.FirstRunNone)
            {
                Size =_playerWindowSize;
                configuration.PlayerSettings.FirstRunNone = false;
            }

            if (configuration.PlayerSettings.CompactPlayer && configuration.PlayerSettings.FirstRunCompactPlayer)
            {
                Size =_windowSizeCompact;
                configuration.PlayerSettings.FirstRunCompactPlayer = false;
            }

            if (configuration.PlayerSettings.NoButtons && configuration.PlayerSettings.FirstRunSetNoButtons)
            {
                Size =_windowSizeNoButtons;
                configuration.PlayerSettings.FirstRunSetNoButtons = false;
            }

            if (configuration.SpotifySettings.LimitedAccess && configuration.PlayerSettings.FirstRunCompactPlayer)
            {
                Size =_windowSizeNoButtons;
                configuration.PlayerSettings.FirstRunCompactPlayer = false;
            }
        }

        private void DrawLocalControls(LocalProvider localProvider, PlayerStateStruct playerState)
        {
            if (!string.IsNullOrEmpty(localProvider.LastAuthError))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
                InterfaceUtils.TextCentered(pageAwareText(localProvider.LastAuthError!));
                ImGui.PopStyleColor();
            }

            var stations = configuration.LocalSettings.Stations;
            if (stations.Count > 0)
            {
                var names = stations.Select(s => s.Name).ToArray();
                if (_localStationIndex >= names.Length)
                {
                    _localStationIndex = 0;
                }

                if (ImGui.Combo("Station", ref _localStationIndex, names, names.Length))
                {
                }

                ImGui.SameLine();
                if (ImGui.Button("Play Station"))
                {
                    var station = stations[_localStationIndex];
                    if (!string.IsNullOrEmpty(station.Url))
                    {
                        localProvider.PlayStation(station.Url);
                    }
                }
            }

            if (!string.IsNullOrEmpty(configuration.LocalSettings.MusicFolder))
            {
                ImGui.SameLine();
                if (ImGui.Button("Play Folder"))
                {
                    localProvider.PlayFolder(configuration.LocalSettings.MusicFolder);
                }
            }

            if (!string.IsNullOrEmpty(configuration.LocalSettings.HttpStreamUrl))
            {
                ImGui.SameLine();
                if (ImGui.Button("Copy Stream Link"))
                {
                    ImGui.SetClipboardText(configuration.LocalSettings.HttpStreamUrl);
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Copies the HTTP stream URL of your mpd server. Paste it into your browser to listen there too.");
                }
            }

            ImGui.Separator();

            if (ImGui.CollapsingHeader("Radio Browser"))
            {
                ImGui.TextWrapped("Search hundreds of thousands of free stations via radio-browser.info.");
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 90);
                ImGui.InputTextWithHint("##radio-search", "Search stations...", ref _radioQuery, 256);
                ImGui.SameLine();
                var topDisabled = _radioBusy;
                if (ImGui.Button("Top") && !topDisabled)
                {
                    _radioBusy = true;
                    _ = Task.Run(() => localProvider.RadioBrowser.TopStationAsync())
                        .ContinueWith(async t =>
                        {
                            _radioResults = t.Status == TaskStatus.RanToCompletion ? t.Result : new List<Config.RadioStation>();
                            _radioBusy = false;
                        }, TaskScheduler.Default);
                }

                ImGui.SameLine();
                if (ImGui.Button("Search") && !string.IsNullOrWhiteSpace(_radioQuery) && !topDisabled)
                {
                    var query = _radioQuery;
                    _radioBusy = true;
                    _ = Task.Run(() => localProvider.RadioBrowser.SearchAsync(query))
                        .ContinueWith(t =>
                        {
                            _radioResults = t.Status == TaskStatus.RanToCompletion ? t.Result : new List<Config.RadioStation>();
                            _radioBusy = false;
                        }, TaskScheduler.Default);
                }

                if (_radioBusy)
                {
                    InterfaceUtils.TextCentered("Searching...");
                }

                if (_radioResults.Count > 0)
                {
                    ImGui.Separator();
                    foreach (var result in _radioResults)
                    {
                        var codec = result.Bitrate > 0
                            ? (string.IsNullOrEmpty(result.Codec) ? result.Bitrate + " kbps" : result.Codec + " " + result.Bitrate + " kbps")
                            : result.Codec;
                        var meta = string.Join(" · ", new[] { result.Country, codec, "♥ " + result.Votes }.Where(s => !string.IsNullOrWhiteSpace(s)));
                        if (ImGui.Selectable($"{result.Name}\n{meta}"))
                        {
                            localProvider.PlayStation(result.Url);
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(result.Url);
                        }
                    }
                }
            }

            ImGui.Separator();
        }

        private void DrawMain(PlayerStateStruct playerState, IPlayerProvider currentProvider)
        {
            BgAlpha = configuration.PlayerSettings.Transparency;

            if (Size != null)
            {
                Size = null;
            }

            //////////////// Right click popup ////////////////

            if (ImGui.BeginPopupContextWindow("RightClick"))
            {
                if (_playerManager.PlayerProviders.Count > 1)
                {
                    if (ImGui.BeginMenu("Switch provider"))
                    {
                        foreach (var provider in _playerManager.PlayerProviders)
                        {
                            if (provider == _playerManager.CurrentPlayerProvider) continue;
                            if (ImGui.MenuItem(provider.Name))
                            {
                                _playerManager.CurrentPlayerProvider = provider;
                                configuration.PlayerSettings.DefaultProvider = provider.Key;
                            }
                        }
                        ImGui.EndMenu();
                    }

                    ImGui.Separator();
                }

                if (!configuration.SpotifySettings.LimitedAccess)
                {
                    var compactPlayer = configuration.PlayerSettings.CompactPlayer;
                    if (ImGui.MenuItem("Compact mode", ref compactPlayer))
                    {
                        if (configuration.PlayerSettings.NoButtons)
                            configuration.PlayerSettings.NoButtons = false;
                        configuration.PlayerSettings.CompactPlayer = compactPlayer;
                    }

                    var noButtons = configuration.PlayerSettings.NoButtons;
                    if (ImGui.MenuItem("Hide Buttons", ref noButtons))
                    {
                        if (configuration.PlayerSettings.CompactPlayer)
                            configuration.PlayerSettings.CompactPlayer = false;
                        configuration.PlayerSettings.NoButtons = noButtons;
                    }

                    ImGui.Separator();
                }

                var playerSettingsPlayerLocked = configuration.PlayerSettings.PlayerLocked;
                if (ImGui.MenuItem("Lock player", ref playerSettingsPlayerLocked))
                {
                    configuration.PlayerSettings.PlayerLocked = playerSettingsPlayerLocked;
                }

                var playerWindowShown = configuration.PlayerSettings.PlayerWindowShown;
                if (ImGui.MenuItem("Show player", ref playerWindowShown))
                {
                    configuration.PlayerSettings.PlayerWindowShown = playerWindowShown;
                }

                var configShown = configuration.ConfigShown;
                if (ImGui.MenuItem("Show config", ref configShown))
                {
                    configuration.ConfigShown = configShown;
                }

                ImGui.EndPopup();
            }

            //////////////// Window Basics ////////////////

            if (currentProvider is LocalProvider localProvider)
            {
                DrawLocalControls(localProvider, playerState);
            }

            if (playerState.CurrentlyPlaying.Id == null)
            {
                InterfaceUtils.TextCentered($"Nothing is playing on {playerState.ServiceName}.");
                return;
            }

            {
                //////////////// Window Setup ////////////////

                ImGui.PushStyleColor(ImGuiCol.Button, InterfaceUtils.TransparentColor);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, InterfaceUtils.TransparentColor);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, InterfaceUtils.DarkenButtonColor);

                var track = playerState.CurrentlyPlaying;



                if (_progressMs != playerState.ProgressMs)
                {
                    _lastUpdated = DateTime.Now;
                    _progressMs = playerState.ProgressMs;
                }
                if (!playerState.IsPlaying)
                {
                    _lastUpdated = null;
                }
                float percent;
                TimeSpan actualProgress;
                TimeSpan songTotal;

                if (_lastUpdated != null)
                {
                    actualProgress = (DateTime.Now - _lastUpdated.Value).Add(TimeSpan.FromMilliseconds(playerState.ProgressMs));
                    songTotal = TimeSpan.FromMilliseconds(track.DurationMs);
                    if (actualProgress >= songTotal)
                    {
                        actualProgress = songTotal;
                    }
                    percent = (float)((double) actualProgress.Ticks / songTotal.Ticks * 100);
                }
                else
                {
                    actualProgress = TimeSpan.FromMilliseconds(playerState.ProgressMs);
                    songTotal = TimeSpan.FromMilliseconds(track.DurationMs);
                    if (actualProgress >= songTotal)
                    {
                        actualProgress = songTotal;
                    }
                    percent = (float)((double) actualProgress.Ticks / songTotal.Ticks * 100);
                }

                var artists = track.Artists.Aggregate("", (current, artist) => current + (artist + ", "));

                if (!configuration.PlayerSettings.NoButtons)
                {
                    //////////////// Play and Pause ////////////////

                    var stateIcon = (playerState.IsPlaying)
                        ? FontAwesomeIcon.Pause.ToIconString()
                        : FontAwesomeIcon.Play.ToIconString();

                    ImGui.PushFont(font.IconFont);

                    if (ImGui.Button(FontAwesomeIcon.Backward.ToIconString()))
                        currentProvider.SetSkip(false);

                    if (InterfaceUtils.ButtonCentered(stateIcon))
                        currentProvider.SetPauseOrPlay(!playerState.IsPlaying);

                    //////////////// Shuffle and Repeat ////////////////

                    ImGui.SameLine(ImGui.GetWindowSize().X / 2 +
                                   (ImGui.GetFontSize() + ImGui.CalcTextSize(FontAwesomeIcon.Random.ToIconString()).X));

                    if (playerState.ShuffleState)
                        ImGui.PushStyleColor(ImGuiCol.Text, configuration.PlayerSettings.AccentColor);

                    if (ImGui.Button(FontAwesomeIcon.Random.ToIconString()))
                        currentProvider.SetShuffle(!playerState.ShuffleState);

                    if (playerState.ShuffleState)
                        ImGui.PopStyleColor();

                    if (playerState.RepeatState != "off")
                        ImGui.PushStyleColor(ImGuiCol.Text, configuration.PlayerSettings.AccentColor);

                    var buttonIcon = FontAwesomeIcon.Retweet.ToIconString();

                    if (playerState.RepeatState == "track")
                        buttonIcon = FontAwesomeIcon.Music.ToIconString();

                    ImGui.SameLine(ImGui.GetWindowSize().X / 2 -
                                   (ImGui.GetFontSize() + ImGui.CalcTextSize(buttonIcon).X +
                                    ImGui.CalcTextSize(FontAwesomeIcon.Random.ToIconString()).X));

                    if (ImGui.Button(buttonIcon))
                        currentProvider.SwapRepeatState();

                    if (playerState.RepeatState != "off")
                        ImGui.PopStyleColor();

                    ImGui.SameLine(ImGui.GetWindowSize().X -
                                   (ImGui.GetFontSize() +
                                    ImGui.CalcTextSize(FontAwesomeIcon.Forward.ToIconString()).X));
                    if (ImGui.Button(FontAwesomeIcon.Forward.ToIconString()))
                        currentProvider.SetSkip(true);

                    ImGui.PopFont();
                }

                if (!configuration.PlayerSettings.CompactPlayer)
                {
                    if (configuration.PlayerSettings.ShowTimeElapsed)
                    {
                        ImGuiUtil.Center(actualProgress.ToString("mm\\:ss", CultureInfo.InvariantCulture) + " / " +
                                         songTotal.ToString("mm\\:ss", CultureInfo.InvariantCulture) +
                                         (playerState.IsPlaying ? "" : " - Paused"));
                    }
                    //////////////// Progress Bar ////////////////

                    ImGui.PushStyleColor(ImGuiCol.PlotHistogram, configuration.PlayerSettings.AccentColor);
                    ImGui.ProgressBar(percent / 100f, new Vector2(-1, 2f));
                    ImGui.PopStyleColor();


                    Vector2 imageSize = new Vector2(100 * ImGui.GetIO().FontGlobalScale,
                        100 * ImGui.GetIO().FontGlobalScale);

                    //////////////// Text ////////////////

                    InterfaceUtils.TextCentered(track.Name);

                    ImGui.PushStyleColor(ImGuiCol.Text, InterfaceUtils.DarkenColor);

                    ImGui.Spacing();
                    InterfaceUtils.TextCentered(artists.Remove(artists.Length - 2));


                    ImGui.PopStyleColor();
                }

                ImGui.PopStyleColor(3);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.uiBuilder.OpenMainUi -= UiBuilderOnOpenMainUi;
            }

            base.Dispose(disposing);
        }
    }
}