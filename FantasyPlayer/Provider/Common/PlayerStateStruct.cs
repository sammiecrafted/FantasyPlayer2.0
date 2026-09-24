using System.Collections.Generic;

namespace FantasyPlayer.Provider.Common
{
    public struct PlayerStateStruct
    {
        public string ServiceName;
        public bool RequiresLogin;
        public bool IsLoggedIn;
        public bool IsAuthenticating;

        public string RepeatState;
        public bool ShuffleState;
        public bool IsPlaying;
        public int ProgressMs;
        public int Volume;
        public TrackStruct CurrentlyPlaying;

        public List<QueueItem> Queue;
        public QueueItem? NextUp;
        public LyricsStruct Lyrics;
        public bool HasQueueSupport;
        public bool HasPlaylistSupport;
        public bool HasLyricsSupport;
    }
}