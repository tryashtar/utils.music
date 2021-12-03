using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TagLib;
using TagLib.Id3v2;
using File = System.IO.File;
using Tag = TagLib.Tag;

namespace TryashtarUtils.Music
{
    public static class LyricsIO
    {
        public static Lyrics? FromMany(IEnumerable<Func<Lyrics?>> methods)
        {
            foreach (var method in methods)
            {
                var lyrics = method();
                if (lyrics != null)
                    return lyrics;
            }
            return null;
        }

        public static Func<Lyrics?> MethodAttempt<T>(Func<T?> setup, Func<T, Lyrics?> getter)
        {
            return () =>
            {
                var item = setup();
                if (item == null)
                    return null;
                return getter(item);
            };
        }

        public const string OGG_LYRICS = "SYNCED_LYRICS";
        public static void ToFile(TagLib.File file, Lyrics? lyrics, string language)
        {
            file.Tag.Lyrics = lyrics?.ToSimple();

            var id3v2 = (TagLib.Id3v2.Tag)file.GetTag(TagTypes.Id3v2);
            var ogg = (TagLib.Ogg.XiphComment)file.GetTag(TagTypes.Xiph);

            if (id3v2 != null)
            {
                foreach (var frame in id3v2.GetFrames<SynchronisedLyricsFrame>().ToList())
                {
                    id3v2.RemoveFrame(frame);
                }
                if (lyrics != null)
                {
                    var new_frame = new SynchronisedLyricsFrame("", language, SynchedTextType.Lyrics, StringType.Latin1)
                    {
                        Text = lyrics.ToSynchedText(),
                        Format = TimestampFormat.AbsoluteMilliseconds
                    };
                    id3v2.AddFrame(new_frame);
                }
            }
            if (ogg != null)
            {
                if (lyrics == null)
                    ogg.RemoveField(OGG_LYRICS);
                else
                    ogg.SetField(OGG_LYRICS, lyrics.ToLrc());
            }
        }

        public static Lyrics? FromFile(TagLib.File file)
        {
            return FromMany(new[] {
                MethodAttempt(() =>
                    (TagLib.Id3v2.Tag)file.GetTag(TagTypes.Id3v2),
                    x => FromId3v2(x)
                ),
                MethodAttempt(() =>
                    (TagLib.Ogg.XiphComment)file.GetTag(TagTypes.Xiph),
                    x => FromXiph(x)
                ),
                MethodAttempt(() =>
                    {
                        var lrc_file = Path.ChangeExtension(file.Name, ".lrc");
                        return File.Exists(lrc_file) ? File.ReadAllLines(lrc_file) : null;
                    },
                    x => FromLrc(x)
                ),
                MethodAttempt(() =>
                    String.IsNullOrEmpty(file.Tag.Lyrics) ? null : file.Tag.Lyrics,
                    x => new Lyrics(x)
                )
            });
        }

        public static Lyrics? FromId3v2(TagLib.Id3v2.Tag tag)
        {
            foreach (var frame in tag.GetFrames<SynchronisedLyricsFrame>())
            {
                return new Lyrics(frame.Text);
            }
            return null;
        }

        public static Lyrics? FromXiph(TagLib.Ogg.XiphComment tag)
        {
            var lyrics = tag.GetField(OGG_LYRICS);
            if (lyrics != null && lyrics.Length > 0)
                return FromLrc(lyrics);
            return null;
        }

        private static readonly Regex LrcRegex = new(@"\[(?<time>.+)\](?<line>.+)");
        private static readonly string[] TimespanFormats = new string[] { @"h\:mm\:ss\.FFF", @"mm\:ss\.FFF", @"m\:ss\.FFF", @"h\:mm\:ss", @"mm\:ss", @"m\:ss" };
        public static Lyrics? FromLrc(string[] lines)
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
            if (list.Count == 0)
                return null;
            return new Lyrics(list);
        }
    }
}
