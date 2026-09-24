# Setting Up Your Own Spotify App for **Fantasy Player**

Due to recent changes in how spotify provides access to it's API you will now need to provide your own client ID to continue to use Fantasy Player.

Fantasy Player integrates with Spotify to enhance your experience, but you’ll need to create your own Spotify application in order to obtain a **Client ID**. This guide walks you through the process step-by-step.

---

## 1. Create a Spotify Developer Account
1. Visit the Spotify Developer Dashboard: https://developer.spotify.com/dashboard
2. Log in with your Spotify account.
3. If prompted, accept the Developer Terms.

---

## 2. Create a New Spotify App
1. In the **Dashboard**, click **"Create app"**.
2. Enter the following:
    - **App Name:** Anything you like (e.g., *Fantasy Player Integration*).
    - The redirect URI should be http://127.0.0.1:2984/callback
    - **App Description:** Optional.
    - Select the Web API checkbox under the `Which API/SDKs are you planning to use?` header 
3. Confirm that you agree to the terms.
4. Click **Create**.

---

## 3. Retrieve Your Client ID
Once the app is created:
1. Open your newly created app.
2. On the **App Overview** page, you will see:
    - **Client ID** → This is what Fantasy Player needs.
    - **Client Secret** → You do *not* need this.

Copy the **Client ID**.

---

## 4. Add Your Client ID to Fantasy Player
1. Type in /pfp config to open Fantasy Player's configuration window.
2. Find the header "Spotify Settings" and open it
3. Locate the field labeled **"Spotify Client ID"**.
4. Paste your copied Client ID into the field.
5. Hit save.

---

## 5. That's It!
Fantasy Player will now let you pick Spotify as a provider and login(you will need to authenticate with your newly created app)

If you ever regenerate your Client ID or create a new Spotify app, just update the field inside the Fantasy Player configuration.

## Troubleshoting
If you put in the wrong Spotify Client ID, put in the correct one, hit save and then reload the plugin.

---

# Free Music: Local Files & Radio (MPD)

If you don't have Spotify Premium (or want a free player), select the **Local** provider on the welcome screen. It plays your own music files and free internet radio through a local **MPD** (Music Player Daemon) — no subscription, no Spotify account needed.

## 1. Install MPD

**Linux / Steam Deck / Lutris** (your game runs under Wine — mpd runs natively on the host OS, which is the reliable way to get sound out):

```sh
# Debian/Ubuntu
sudo apt install mpd
# Fedora
sudo dnf install mpd
# Arch
sudo pacman -S mpd
```

**Windows:** download the Windows build from https://www.musicpd.org/download/windows/ and run it, or install via a package manager (e.g. `choco install mpd`).

## 2. Configure MPD as your own music server

MPD is your own tiny music server. The plugin connects to it at `127.0.0.1:6600` and tells it what to play; MPD does the playing to your speakers.

Create `~/.config/mpd/mpd.conf` (Linux) or `C:\Users\you\AppData\Roaming\mpd\mpd.conf` (Windows).

**Worked example** — say your music lives in `/home/you/Music` on Linux:

```
music_directory  "/home/you/Music"          # your own music library
playlist_directory  "/home/you/Music/playlists"
db_file     "/home/you/.cache/mpd/tag_cache"
log_file    "/home/you/.cache/mpd/mpd.log"
state_file  "/home/you/.cache/mpd/state"
bind_to_address  "127.0.0.1"                 # only local, keep it private
port         "6600"

audio_output {
    type  "pulse"
    name  "Pulse Output"
}

# Optional: let you ALSO listen in a browser. After restarting mpd, the
# "Copy Stream Link" button in the plugin copies http://127.0.0.1:8000/ to
# your clipboard; paste it into any browser to hear whatever mpd is playing.
audio_output {
    type  "httpd"
    name  "My HTTP Stream"
    encoder  "vorbis"
    port     "8000"
    bind_to_address  "127.0.0.1"
}

# Windows example:
# music_directory "C:/Users/you/Music"
# audio_output { type "wasapi" name "WASAPI" }
```

Make the cache folder first if it doesn't exist: `mkdir -p ~/.cache/mpd`

## 3. Start MPD and check it is listening

```sh
# run once in the background (or add it to your startup)
mpd ~/.config/mpd/mpd.conf

# verify it is listening on port 6600
ss -tlnp | grep 6600    # or: netstat -tlnp | grep 6600
```

If you get an "address already in use" error, mpd is already running.

## 4. Tell the plugin where your server is

1. In game, open `/pfp config` → **Local Music (MPD) Settings**.
2. **MPD host** `127.0.0.1`, **MPD port** `6600` (leave password empty unless you set one).
3. **Music folder**: the path mpd can read — usually the same as `music_directory`, e.g. `/home/you/Music`.
4. Close settings. On the welcome screen pick **Local** as your provider.

## 5. Play your own music files

On the floating player, click **Play Folder** (button appears next to the Station picker). It tells MPD to load your whole music folder and plays it in shuffle/repeat. Use the player buttons: **play / pause / stop / skip / shuffle / repeat**, and enter `/pfp volume 50` or drag in the settings.

## 6. Add a live radio station from a website

**Easiest way — the Radio Browser panel** (no manual copying):

1. On the floating player, expand **Radio Browser**.
2. Click **Top** for popular stations worldwide, or type a genre in the search box (e.g. `lofi`, `jazz`, `classical`) and click **Search**.
3. Click any row — it starts playing immediately (country / codec / votes are shown next to each name).

**Manual way — from any radio website:**

1. Find the live radio station you want (e.g. from https://www.radio-browser.info, a station's website, or a streaming directory).
2. Get the **direct stream URL**:
   - On many "Listen live" pages: right-click the Play button → **Copy link address**.
   - If you get a `.pls` or `.m3u` file, open it in a text editor — it contains a line like `File1=http://...mp3`; use **that** URL.
3. Example of a real, always-free station (Radio Paradise):
   ```
   https://stream.radioparadise.com/mp3-192
   ```
4. In `/pfp config` → **Local Music (MPD) Settings** → **Radio stations** → **Add station**, enter a Name and the URL, then hit Done.
5. On the floating player, pick it from the **Station** dropdown → **Play Station**.

## 7. The floating player box (show/hide, transparency, click-through)

- Show/hide the floating box: `/pfp config` → **Fantasy Player** → **Show floating player** / **Hide floating player** (or the `/pfp display` command).
- Make the box see-through: `/pfp config` → **Player Settings** → **Player alpha** slider (lower = more transparent).
- Click-through (mouse ignores the box): **Player Settings** → **Player input disabled**.
- Right-click the floating player for quick access to **Switch provider**, **Lock player**, **Show player**, **Show config**.

