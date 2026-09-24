using System.Collections.Generic;

namespace FantasyPlayer.Provider.Common
{
    public struct PlaylistStruct
    {
        public string Id;
        public string Name;
        public string Description;
        public string ImageUrl;
        public int TrackCount;
        public string Owner;
    }

    public struct PlaylistItem
    {
        public string Id;
        public string Name;
        public string[] Artists;
        public AlbumStruct Album;
        public int DurationMs;
        public string ImageUrl;
        public string PlaylistId;
    }

    public struct PlaylistTrackList
    {
        public PlaylistStruct Playlist;
        public List<PlaylistItem> Tracks;
    }
}
