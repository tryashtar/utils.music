using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TagLib.Id3v2;
using TryashtarUtils.Utility;

namespace TryashtarUtils.Music
{
    public class Lyrics
    {
        public readonly bool Synchronized;
        private readonly List<LyricsEntry> Entries = new List<LyricsEntry>();
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

        private static readonly Regex LrcRegex = new Regex(@"\[(?<time>.+)\](?<line>.+)");
        private static readonly string[] TimespanFormats = new string[] { @"h\:mm\:ss\.FFF", @"mm\:ss\.FFF", @"m\:ss\.FFF", @"h\:mm\:ss", @"mm\:ss", @"m\:ss" };
        public static Lyrics FromLrc(string[] lines)
        {
            var list = new List<LyricsEntry>();
            foreach (var line in lines)
            {
                var match = LrcRegex.Match(line);
                if (match.Success)
                {
                    if (TimeSpan.TryParseExact(match.Groups["time"].Value, TimespanFormats, null, out var time))
                        list.Add(new LyricsEntry(match.Groups["line"].Value, time));
                }
            }
            return new Lyrics(list);
        }

        public SynchedText[] ToSynchedText()
        {
            return Entries.Select(x => new SynchedText((long)x.Time.TotalMilliseconds, x.Text)).ToArray();
        }

        public string ToSimple()
        {
            return String.Join(Environment.NewLine, Entries.Select(x => x.Text));
        }

        public string ToLrc()
        {
            return String.Join(Environment.NewLine, Entries.Select(x => x.ToLrcEntry()));
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
            string time_str = Time.TotalHours < 1 ? Time.ToString(@"mm\:ss\.ff") : Time.ToString(@"h\:mm\:ss\.ff");
            return $"[{time_str}]{Text}";
        }
    }
}
