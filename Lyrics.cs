using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TagLib.Id3v2;
using TryashtarUtils.Utility;

namespace TryashtarUtils.Music
{
    public class Lyrics
    {
        public readonly bool Synchronized;
        private readonly List<LyricsEntry> Entries = new();
        public ReadOnlyCollection<LyricsEntry> Lines => Entries.AsReadOnly();

        public Lyrics(SynchedText[] text)
        {
            Synchronized = true;
            foreach (var item in text)
            {
                Entries.Add(new LyricsEntry(item.Text, TimeSpan.FromMilliseconds(item.Time)));
            }
        }

        public Lyrics(IEnumerable<LyricsEntry> entries)
        {
            Synchronized = true;
            foreach (var item in entries)
            {
                Entries.Add(item);
            }
        }

        public Lyrics(string lyrics)
        {
            Synchronized = false;
            foreach (var line in StringUtils.SplitLines(lyrics))
            {
                Entries.Add(new LyricsEntry(line, TimeSpan.Zero));
            }
        }

        private static readonly IComparer<LyricsEntry> TimeComparer = new LambdaComparer<LyricsEntry, TimeSpan>(x => x.Time);

        public LyricsEntry? LyricAtTime(TimeSpan time)
        {
            var fake = new LyricsEntry("", time);
            int search = Entries.BinarySearch(fake, TimeComparer);
            if (search < 0)
            {
                search = ~search;
                search--;
            }
            if (search < 0)
                return null;
            search = Math.Min(search, Entries.Count - 1);
            return Entries[search];
        }

        public SynchedText[] ToSynchedText()
        {
            return Entries.Select(x => new SynchedText((long)x.Time.TotalMilliseconds, x.Text)).ToArray();
        }

        public string ToSimple()
        {
            return String.Join("\n", Entries.Select(x => x.Text));
        }

        public string[] ToLrc()
        {
            return Entries.Select(x => x.ToLrcEntry()).ToArray();
        }
    }

    public struct LyricsEntry
    {
        public readonly string Text;
        public readonly TimeSpan Time;
        public LyricsEntry(string text, TimeSpan time)
        {
            Text = text;
            Time = time;
        }

        public string ToLrcEntry()
        {
            string time_str = StringUtils.TimeSpan(Time);
            return $"[{time_str}]{Text}";
        }
    }
}
