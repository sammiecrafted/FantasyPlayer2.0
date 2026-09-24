using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using FantasyPlayer.Config;
using FantasyPlayer.Provider;
using Microsoft.Extensions.Logging;

namespace FantasyPlayer.Interface.Window
{
    using DalaMock.Host.Mediator;
    using Dalamud.Interface.Utility.Raii;
    using Dalamud.Interface.Windowing;

    public class SpotifyLoginWindow : UpdatingWindow
    {
        private readonly SpotifyProvider spotifyProvider;
        private readonly Configuration configuration;
        private string _manualCode = "";
        private bool _autoStarted;

        public SpotifyLoginWindow(ILogger<SpotifyLoginWindow> logger, MediatorService mediatorService, SpotifyProvider spotifyProvider, Configuration configuration)
            : base(logger, mediatorService, "Fantasy Player - Spotify Login")
        {
            this.spotifyProvider = spotifyProvider;
            this.configuration = configuration;
            this.Size = new System.Numerics.Vector2(560, 420);
            this.SizeCondition = ImGuiCond.FirstUseEver;
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new System.Numerics.Vector2(420, 340)
            };
        }

        public void Open()
        {
            IsOpen = true;
            _autoStarted = false;
        }

        public override bool DrawConditions()
        {
            return IsOpen;
        }

        public override void OnClose()
        {
            _autoStarted = false;
            base.OnClose();
        }

        public override void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            if (spotifyProvider.PlayerState.IsLoggedIn)
            {
                IsOpen = false;
                return;
            }

            if (!_autoStarted && spotifyProvider.Initialized &&
                !string.IsNullOrEmpty(configuration.SpotifySettings.SpotifyClientId) &&
                !spotifyProvider.PlayerState.IsAuthenticating)
            {
                _autoStarted = true;
                spotifyProvider.StartAuth();
            }
        }

        public override void Draw()
        {
            if (!spotifyProvider.Initialized)
            {
                InterfaceUtils.TextCentered("The Spotify provider is still loading, please wait...");
                return;
            }

            var playerProvider = spotifyProvider;

            if (string.IsNullOrEmpty(configuration.SpotifySettings.SpotifyClientId))
            {
                InterfaceUtils.TextCentered("No Spotify Client ID configured yet.");
                InterfaceUtils.TextCentered("Add one under /pfp config -> Spotify Settings (see SETUP.md).");
                if (InterfaceUtils.ButtonCentered("Open Settings"))
                {
                    configuration.ConfigShown = true;
                }
                return;
            }

            if (!playerProvider.PlayerState.IsAuthenticating)
            {
                InterfaceUtils.TextCentered($"Please login to {playerProvider.PlayerState.ServiceName} to start.");
                if (InterfaceUtils.ButtonCentered("Login"))
                    playerProvider.StartAuth();
            }
            else
            {
                InterfaceUtils.TextCentered("Waiting for a response to login... Copy the URL below, open it,");
                InterfaceUtils.TextCentered("and authorize with the account you want to use.");
                if (InterfaceUtils.ButtonCentered("Re-open Url"))
                    playerProvider.RetryAuth();

                var authUri = playerProvider.AuthUri;
                if (!string.IsNullOrEmpty(authUri))
                {
                    if (InterfaceUtils.ButtonCentered("Copy Authorize URL"))
                    {
                        ImGui.SetClipboardText(authUri);
                    }
                }

                ImGui.Spacing();
                if (InterfaceUtils.ButtonCentered("Complete Login") && !string.IsNullOrWhiteSpace(_manualCode))
                {
                    playerProvider.CompleteAuth(_manualCode);
                }
                if (InterfaceUtils.ButtonCentered("Paste & Complete Login"))
                {
                    _manualCode = ImGui.GetClipboardText() ?? string.Empty;
                    playerProvider.CompleteAuth(_manualCode);
                }

                ImGui.Separator();
                InterfaceUtils.TextCentered("After you click Agree, your browser can't connect back to the game's local");
                InterfaceUtils.TextCentered("server (normal on Wine/Linux), but the address bar still has the login code. Copy the");
                InterfaceUtils.TextCentered("full 127.0.0.1:2984/callback?code=... URL and click \"Paste & Complete Login\".");
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                ImGui.InputTextWithHint("##pfp-login-code", "Paste login code or callback URL", ref _manualCode, 8192);
                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Text, InterfaceUtils.DarkenColor);
                InterfaceUtils.TextCentered("Tip: Spotify requires the account that OWNS the app (Client ID) to have an active");
                InterfaceUtils.TextCentered("Premium subscription. If the owner account is Free, login will fail.");
                ImGui.PopStyleColor();
            }

            if (!string.IsNullOrEmpty(playerProvider.LastAuthError))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
                InterfaceUtils.TextCentered(PageAwareText(playerProvider.LastAuthError!));
                ImGui.PopStyleColor();
                if (InterfaceUtils.ButtonCentered("Retry Connection"))
                {
                    playerProvider.ClearAuthError();
                    playerProvider.RetryConnect();
                }
                if (InterfaceUtils.ButtonCentered("Dismiss"))
                {
                    playerProvider.ClearAuthError();
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            if (InterfaceUtils.ButtonCentered("Reset Login"))
            {
                _manualCode = string.Empty;
                playerProvider.ResetLogin();
            }
        }

        private string PageAwareText(string input) => input.Length > 400 ? input.Substring(0, 400) + "..." : input;
    }
}