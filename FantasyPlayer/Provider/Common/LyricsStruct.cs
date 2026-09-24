using System.Collections.Generic;

namespace FantasyPlayer.Provider.Common
{
    public struct LyricsLine
    {
        public int TimeMs;
        public string Text;
    }

    public struct LyricsStruct
    {
        public string TrackId;
        public string TrackName;
        public string Artist;
        public List<LyricsLine> Lines;
        public bool IsSynced;
    }
}
