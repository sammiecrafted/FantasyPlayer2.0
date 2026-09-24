# Fantasy Player Improved

<p align="center">
  <img src="./.repo_resources/logo.png" alt="Fantasy Player Improved Logo" width="128" height="128" />
</p>

<p align="center">
  <strong>A Dalamud plugin to control your music from within Final Fantasy XIV.</strong>
</p>

<p align="center">
  <a href="#features">Features</a> •
  <a href="#install-in-game">Install</a> •
  <a href="#setup">Setup</a> •
  <a href="#providers">Providers</a> •
  <a href="#building">Building</a>
</p>

---

## Features

- **Multi-provider support** — Spotify, Apple Music, and a free Local/MPD provider
- **Hardened OAuth login** — Works on Wine/Linux and behind firewalls with manual code paste fallback
- **In-game chat song announcements** — See what's playing without leaving the game
- **Inter-plugin communication (IPC)** — Let other plugins read now-playing info
- **Floating player window** — Compact, minimal, or full layout with custom colors and transparency
- **Free music** — No subscription needed: play local files and internet radio via MPD

## Providers

| Provider | Requires Login | Premium Required | Queue | Playlists | Lyrics |
|----------|:---:|:---:|:---:|:---:|:---:|
| **Spotify** | Yes | Yes (for playback) | Yes | Yes | Yes |
| **Apple Music** | Yes (Dev Token) | No (catalog search) | Yes | Yes | Yes |
| **Local (MPD)** | No | No | Yes | Yes | — |

## Install in-game

1. Open your Dalamud plugin window (`/xlplugins`)
2. Click the gear icon → **Third-Party Repositories**
3. Add this repository URL:

   ```
   https://raw.githubusercontent.com/sammiecrafted/FantasyPlayer2.0/main/repo.json
   ```

   *(Alternative CDN, may lag behind: `https://cdn.jsdelivr.net/gh/sammiecrafted/FantasyPlayer2.0@main/repo.json`)*

4. In the **Available Plugins** tab, search for **Fantasy Player Improved** and click **Install**

## Setup

### Spotify

Spotify requires you to bring your own Developer app. Follow [SETUP.md](./SETUP.md) to create one and get your Client ID.

> **Note:** You must have **Spotify Premium** to use playback controls.

### Apple Music

1. Create an Apple Developer account at [developer.apple.com](https://developer.apple.com)
2. Generate a **MusicKit developer token** (JWT) in the Apple Developer portal
3. Enter the token, your **Team ID**, and **Key ID** in the Apple Music settings tab
4. *(Optional)* Provide a Music User Token for library and personal playlist access

### Local / MPD (Free)

No account needed. Install `mpd` (Music Player Daemon) on your machine and configure the host/port in settings. See [SETUP.md](./SETUP.md) for details.

## Commands

Use `/pfp` in-game to control playback:

| Command | Aliases | Description |
|---------|---------|-------------|
| `/pfp help` | — | Show all commands |
| `/pfp config` | `settings` | Toggle config window |
| `/pfp shuffle` | — | Toggle shuffle |
| `/pfp next` | `skip` | Skip to next track |
| `/pfp back` | `previous` | Go back a track |
| `/pfp pause` | `stop` | Pause playback |
| `/pfp play` | — | Resume playback |
| `/pfp volume <0-100>` | — | Set volume |
| `/pfp display` | — | Toggle player window |
| `/pfp relogin` | `reauth` | Re-open login window |
| `right click` | `n/a` | Right Click player for more options. |

## Building

```sh
git clone https://github.com/sammiecrafted/FantasyPlayer2.0.git
cd FantasyPlayer2.0
git submodule update --init --recursive
dotnet build FantasyPlayer/FantasyPlayer.csproj -c Release
```

## Current Version

[View Releases](https://github.com/sammiecrafted/FantasyPlayer2.0/releases)

## License

This project is licensed under the GNU General Public License v3.0 — see [LICENSE.md](./LICENSE.md) for details.

## Credits

- Fork of [Critical-Impact/FantasyPlayer](https://github.com/Critical-Impact/FantasyPlayer)
- Uses [SpotifyAPI-NET](https://github.com/JohnnyCrazy/SpotifyAPI-NET) for Spotify integration
- Uses [OtterGui](https://github.com/Ottermandias/OtterGui) for UI widgets
- Built on the [Dalamud](https://github.com/goatcorp/Dalamud) plugin framework
