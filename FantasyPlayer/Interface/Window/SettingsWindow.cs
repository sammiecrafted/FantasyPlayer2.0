using Dalamud.Bindings.ImGui;
using System.Numerics;
using AllaganLib.Shared.Extensions;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using FantasyPlayer.Interfaces;
using FantasyPlayer.Manager;
using Microsoft.Extensions.Logging;
using OtterGui.Widgets;

namespace FantasyPlayer.Interface.Window
{
    using Config;
    using DalaMock.Host.Mediator;
    using Dalamud.Interface.Windowing;
    using Dalamud.Plugin.Services;
    using FantasyPlayer.Mediator;
    using Serilog;

    public class SettingsWindow : UpdatingWindow
    {
        private readonly Configuration _configuration;
        private readonly ConfigurationManager _configurationManager;
        private readonly CommandManagerFp commandManagerFp;
        private readonly IUiBuilder _uiBuilder;

        public SettingsWindow(ILogger<SettingsWindow> logger, MediatorService mediatorService, Configuration configuration, ConfigurationManager configurationManager, CommandManagerFp commandManagerFp, IUiBuilder uiBuilder) : base(logger, mediatorService, "Fantasy Player - Configuration", ImGuiWindowFlags.NoScrollbar)
        {
            _configuration = configuration;
            _configurationManager = configurationManager;
            this.commandManagerFp = commandManagerFp;
            _uiBuilder = uiBuilder;
            MediatorService.Subscribe<ConfigurationUpdatedMessage>(this, ConfigurationUpdated );
            this.Size = new Vector2(800, 800);
            this.SizeCondition = ImGuiCond.FirstUseEver;
            this.SizeConstraints = new WindowSizeConstraints()
            {
                MinimumSize = new Vector2(600, 400)
            };
        }

        public override void OnClose()
        {
            _configuration.ConfigShown = false;
            base.OnClose();
        }

        private void ConfigurationUpdated(ConfigurationUpdatedMessage obj)
        {
            if (_configuration.ConfigShown && !IsOpen)
            {
                IsOpen = true;
            }
            if (!_configuration.ConfigShown && IsOpen)
            {
                IsOpen = false;
            }
        }

        public override bool DrawConditions()
        {
            if (!_configuration.ConfigShown)
            {
                return false;
            }

            return base.DrawConditions();
        }

        public override void Draw()
        {
            MainWindow();
        }

        public override void Update()
        {

        }

        private string? spotifyClientId = null;

        private void MainWindow()
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
            ImGui.TextWrapped("Due to recent changes in how spotify provides access to it's API you will now need to provide your own client ID to continue to use Fantasy Player. Please see the Spotify Settings section for more details.");
            ImGui.PopStyleColor();
            ImGui.NewLine();
            ImGui.PushStyleColor(ImGuiCol.Text, InterfaceUtils.DarkenColor);
            ImGui.Text($"Type '{commandManagerFp.Command} help' to display chat commands!");
            ImGui.PopStyleColor();

            if (ImGui.CollapsingHeader("Fantasy Player"))
            {
                var displayChatMessages = _configuration.DisplayChatMessages;
                if (ImGui.Checkbox("Display chat messages", ref displayChatMessages))
                {
                    _configuration.DisplayChatMessages = displayChatMessages;
                }
                if (Widget.DrawChatTypeSelector("Chat message output channel",
                        "To which chat channel should the fantasy player messages be echo'd?",
                        _configuration.PlayerSettings.ChatType,
                        type => { _configuration.PlayerSettings.ChatType = type; }))
                {
                }
            }

            if (ImGui.CollapsingHeader("Spotify Settings"))
            {
                if (this.spotifyClientId == null)
                {
                    this.spotifyClientId = _configuration.SpotifySettings.SpotifyClientId;
                }
                if (ImGui.InputText("Spotify Client ID",
                        ref this.spotifyClientId))
                {

                }

                ImGui.SameLine();

                if (ImGui.Button("Save") && !string.IsNullOrEmpty(this.spotifyClientId))
                {
                    _configuration.SpotifySettings.SpotifyClientId = this.spotifyClientId;
                }

                var setupUrl = "https://github.com/sammiecrafted/FantasyPlayer2.0/blob/main/SETUP.md";
                if (ImGui.Button("Copy Instructions Link"))
                {
                    ImGui.SetClipboardText(setupUrl);
                }

                ImGui.SameLine();

                if (ImGui.Button("Open Instructions"))
                {
                    setupUrl.OpenBrowser();
                }
            }

            if (ImGui.CollapsingHeader("Local Music (MPD) Settings"))
            {
                ImGui.TextWrapped("For free music without Spotify: this plays your own music files and free internet radio through a local MPD (Music Player Daemon). Select the 'Local' provider on the welcome screen. See SETUP.md for how to install mpd.");
                ImGui.Separator();

                var host = _configuration.LocalSettings.Host;
                if (ImGui.InputText("MPD host", ref host, 128))
                {
                    _configuration.LocalSettings.Host = host;
                }

                var port = _configuration.LocalSettings.Port;
                if (ImGui.InputInt("MPD port", ref port))
                {
                    _configuration.LocalSettings.Port = port;
                }

                var password = _configuration.LocalSettings.Password;
                if (ImGui.InputText("MPD password (optional)", ref password, 128, ImGuiInputTextFlags.Password))
                {
                    _configuration.LocalSettings.Password = password;
                }

                var musicFolder = _configuration.LocalSettings.MusicFolder;
                if (ImGui.InputText("Music folder", ref musicFolder, 512))
                {
                    _configuration.LocalSettings.MusicFolder = musicFolder;
                }

                ImGui.TextWrapped("The music folder must be a path mpd can read (keep it inside mpd's music_directory).");

                ImGui.Separator();

                ImGui.Text("Radio stations:");
                var toRemove = -1;
                for (var i = 0; i < _configuration.LocalSettings.Stations.Count; i++)
                {
                    var station = _configuration.LocalSettings.Stations[i];
                    var name = station.Name;
                    var url = station.Url;
                    ImGui.PushID($"station{i}");
                    if (ImGui.InputTextWithHint($"##station-name{i}", "Name", ref name, 128))
                    {
                        _configuration.LocalSettings.EditStation(i, name, url);
                    }

                    if (ImGui.InputTextWithHint($"##station-url{i}", "http://...", ref url, 512))
                    {
                        _configuration.LocalSettings.EditStation(i, name, url);
                    }

                    ImGui.SameLine();
                    if (ImGui.Button("Remove"))
                    {
                        toRemove = i;
                    }

                    ImGui.PopID();
                }

                if (toRemove >= 0)
                {
                    _configuration.LocalSettings.RemoveStationAt(toRemove);
                }

                if (ImGui.Button("Add station"))
                {
                    _configuration.LocalSettings.AddStation(new FantasyPlayer.Config.RadioStation());
                }
            }

            if (!_configuration.SpotifySettings.LimitedAccess)
            {
                if (ImGui.CollapsingHeader("Auto-play Settings"))
                {
                    var playInDuty = _configuration.AutoPlaySettings.PlayInDuty;
                    if (ImGui.Checkbox("Auto play when entering Duty",
                            ref playInDuty))
                    {
                        _configuration.AutoPlaySettings.PlayInDuty = playInDuty;
                    }
                }
            }

            if (ImGui.CollapsingHeader("Player Settings"))
            {
                if (_configuration.SpotifySettings.LimitedAccess)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, InterfaceUtils.DarkenColor);
                    ImGui.Text("You're not premium on Spotify. Some settings have been hidden.");
                    ImGui.PopStyleColor();
                }

                ImGui.Separator();

                var onlyWhenLoggedIn = _configuration.PlayerSettings.OnlyOpenWhenLoggedIn;
                if (ImGui.Checkbox("Only open when logged in",
                        ref onlyWhenLoggedIn))
                {
                    _configuration.PlayerSettings.OnlyOpenWhenLoggedIn = onlyWhenLoggedIn;
                }

                ImGui.Separator();

                if (!_configuration.SpotifySettings.LimitedAccess)
                {
                    var compactPlayer = _configuration.PlayerSettings.CompactPlayer;
                    if (ImGui.Checkbox("Compact mode", ref compactPlayer))
                    {
                        if (_configuration.PlayerSettings.NoButtons)
                            _configuration.PlayerSettings.NoButtons = false;
                        _configuration.PlayerSettings.CompactPlayer = compactPlayer;
                    }

                    var noButtons = _configuration.PlayerSettings.NoButtons;
                    if (ImGui.Checkbox("Hide buttons", ref noButtons))
                    {
                        if (_configuration.PlayerSettings.CompactPlayer)
                            _configuration.PlayerSettings.CompactPlayer = false;
                        _configuration.PlayerSettings.NoButtons = noButtons;
                    }
                }

                ImGui.Separator();

                var playerWindowShown = _configuration.PlayerSettings.PlayerWindowShown;
                if (ImGui.Checkbox("Player shown", ref playerWindowShown))
                {
                    _configuration.PlayerSettings.PlayerWindowShown = playerWindowShown;
                }

                var playerLocked = _configuration.PlayerSettings.PlayerLocked;
                if (ImGui.Checkbox("Player locked", ref playerLocked))
                {
                    _configuration.PlayerSettings.PlayerLocked = playerLocked;
                }

                var disableInput = _configuration.PlayerSettings.DisableInput;
                if (ImGui.Checkbox("Player input disabled", ref disableInput))
                {
                    _configuration.PlayerSettings.DisableInput = disableInput;
                }

                var timeElapsed = _configuration.PlayerSettings.ShowTimeElapsed;
                if (ImGui.Checkbox("Show time elapsed",
                        ref timeElapsed))
                {
                    _configuration.PlayerSettings.ShowTimeElapsed = timeElapsed;
                }

                ImGui.Separator();

                var transparency = _configuration.PlayerSettings.Transparency;
                if (ImGui.SliderFloat("Player alpha", ref transparency, 0f,
                        1f))
                {
                    _configuration.PlayerSettings.Transparency = transparency;
                }

                var accentColor = _configuration.PlayerSettings.AccentColor;
                if (ImGui.ColorEdit4("Player color", ref accentColor))
                {
                    _configuration.PlayerSettings.AccentColor = accentColor;
                }

                ImGui.NewLine();
                if (ImGui.Button("Revert"))
                {
                    _configuration.PlayerSettings.AccentColor = InterfaceUtils.FantasyPlayerColor;
                    _configuration.PlayerSettings.FirstRunNone = true;
                }
                ImGui.SameLine();
                ImGui.Text("Revert to default size");

                ImGui.Separator();
                if (ImGui.Button("Compact mode"))
                {
                    _configuration.PlayerSettings.FirstRunCompactPlayer = true;
                }

                ImGui.SameLine();
                if (ImGui.Button("Hide buttons"))
                {
                    _configuration.PlayerSettings.FirstRunSetNoButtons = true;
                }

                ImGui.SameLine();
                if (ImGui.Button("Full"))
                {
                    _configuration.PlayerSettings.FirstRunNone = true;
                }



                ImGui.Separator();

                var debugWindowOpen = _configuration.PlayerSettings.DebugWindowOpen;
                if (ImGui.Checkbox("Show debug window", ref debugWindowOpen))
                {
                    _configuration.PlayerSettings.DebugWindowOpen = debugWindowOpen;
                }
            }

            ImGui.Separator();
        }
    }
}