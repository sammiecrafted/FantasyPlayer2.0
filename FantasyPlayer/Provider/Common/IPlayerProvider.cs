using System.Collections.Generic;
using System.Threading.Tasks;
using FantasyPlayer.Interface;
using FantasyPlayer.Interfaces;

namespace FantasyPlayer.Provider.Common
{
    using System;
    using Microsoft.Extensions.Hosting;

    public interface IPlayerProvider : IHostedService, IDisposable
    {
        public PlayerStateStruct PlayerState { get; set; }
        public Task<IPlayerProvider> Initialize();
        public string Key { get; }
        public string Name { get; }
        public bool Initialized { get; }
        public void Update();
        public void StartAuth();
        public void RetryAuth();
        public void ReAuth();

        public string? AuthUri { get; }
        public string? LastAuthError { get; }
        public void CompleteAuth(string code);
        public void ClearAuthError();
        public void RetryConnect();
        public void ResetLogin();

        public void SwapRepeatState();
        public void SetPauseOrPlay(bool play);
        public void SetSkip(bool forward);
        public void SetShuffle(bool value);
        public void SetVolume(int volume);

        public void Seek(int positionMs);
        public void AddToQueue(string trackId);
        public Task<List<QueueItem>> GetQueue();
        public void RemoveFromQueue(int index);

        public Task<List<PlaylistStruct>> GetPlaylists();
        public Task<PlaylistTrackList> GetPlaylistTracks(string playlistId);
        public Task<PlaylistTrackList> SearchPlaylists(string query);
        public void PlayPlaylist(string playlistId, int trackIndex = 0);
        public Task<List<QueueItem>> SearchTracks(string query);

        public Task<LyricsStruct> GetLyrics();

        public void SetPlayerVolume(string playerName, int volume);
        public int GetPlayerVolume(string playerName);
    }
}