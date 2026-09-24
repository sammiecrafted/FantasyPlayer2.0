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

## 2. Configure MPD

Create `~/.config/mpd/mpd.conf` (Linux) or `C:\Users\you\AppData\Roaming\mpd\mpd.conf` (Windows) with at least:

```
music_directory  "/home/you/Music"          # where your music lives (MPD must be able to read it)
playlist_directory  "/home/you/Music/playlists"
db_file     "/tmp/mpd.db"
log_file    "/tmp/mpd.log"
state_file  "/tmp/mpd.state"

audio_output {
    type  "pulse"
    name  "Pulse Output"
}
# Windows example:
# audio_output { type "wasapi" name "WASAPI" }
```

Start it (`systemctl --user start mpd` for a user service, or just run `mpd` in a terminal). Make sure it is listening on port **6600**.

## 3. Wire it up in the plugin

1. In game, open `/pfp config` → **Local Music (MPD) Settings**.
2. Set the mpd host (`127.0.0.1`), port (default `6600`), and password if you set one.
3. Set **Music folder** to a path mpd can read (usually inside `music_directory`).
4. Add your favorite free radio stations (name + stream URL, e.g. from https://www.radio-browser.info). The URL should end in `.mp3`, `.ogg`, `.aac`, or `.pls` for best results.
5. Close settings, then select **Local** as your provider on the welcome screen.
6. On the player window, choose a **Station** → **Play Station**, or **Play Folder** to play your own files.

The player controls (play/pause, skip, shuffle, repeat, volume) work exactly like Spotify. If you get "Could not connect to MPD", check mpd is running and the host/port match.

