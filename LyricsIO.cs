using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TagLib;
using TagLib.Id3v2;
using TryashtarUtils.Utility;
using File = System.IO.File;
using Tag = TagLib.Tag;

namespace TryashtarUtils.Music
{
    public static class LyricsIO
    {
        public const string OGG_LYRICS = "LYRICS";
        public const string OGG_UNSYNCED_LYRICS = "UNSYNCED LYRICS";
        public static bool ToFile(TagLib.File file, Lyrics? lyrics)
        {
            bool changed = false;
            var simple = lyrics?.ToSimple();

            var id3v2 = (TagLib.Id3v2.Tag)file.GetTag(TagTypes.Id3v2);
            var ogg = (TagLib.Ogg.XiphComment)file.GetTag(TagTypes.Xiph);
            var ape = (TagLib.Ape.Tag)file.GetTag(TagTypes.Ape);

            if (id3v2 != null)
            {
                var synced_frames = id3v2.GetFrames<SynchronisedLyricsFrame>().ToList();
                var unsynced_frames = id3v2.GetFrames<UnsynchronisedLyricsFrame>().ToList();
                foreach (var frame in synced_frames)
                {
                    id3v2.RemoveFrame(frame);
                }
                foreach (var frame in unsynced_frames)
                {
                    id3v2.RemoveFrame(frame);
                }
                changed |= synced_frames.Count > 1 || unsynced_frames.Count > 1;
                if (lyrics != null)
                {
                    string? language = Language.Get(id3v2) ?? "XXX";
                    var synced_frame = new SynchronisedLyricsFrame("", language, SynchedTextType.Lyrics, StringType.Latin1)
                    {
                        Text = lyrics.ToSynchedText(),
                        Format = TimestampFormat.AbsoluteMilliseconds
                    };
                    var unsynced_frame = new UnsynchronisedLyricsFrame("", language, StringType.Latin1)
                    {
                        Text = lyrics.ToSimple()
                    };
                    id3v2.AddFrame(synced_frame);
                    id3v2.AddFrame(unsynced_frame);
                    // use || instead of |= to short-circuit
                    changed = changed || synced_frames.Count == 0 || unsynced_frames.Count == 0
                        || !IdenticalFrames(synced_frames[0], synced_frame) || !IdenticalFrames(unsynced_frames[0], unsynced_frame);
                }
                else
                    changed |= synced_frames.Count > 0 || unsynced_frames.Count > 0;
            }
            if (ape != null)
            {
                changed |= ape.Lyrics != simple;
                ape.Lyrics = simple;
            }
            if (ogg != null)
            {
                if (lyrics == null)
                {
                    changed |= ogg.GetFirstField(OGG_LYRICS) != null;
                    ogg.RemoveField(OGG_LYRICS);
                    changed |= ogg.GetFirstField(OGG_UNSYNCED_LYRICS) != null;
                    ogg.RemoveField(OGG_UNSYNCED_LYRICS);
                }
                else
                {
                    var lrc = String.Join("\n", lyrics.ToLrc());
                    var existing = ogg.GetField(OGG_LYRICS);
                    changed |= existing.Length != 1 || existing[0] != lrc;
                    ogg.SetField(OGG_LYRICS, lrc);

                    var existing_unsynced = ogg.GetField(OGG_UNSYNCED_LYRICS);
                    changed |= existing_unsynced.Length != 1 || existing_unsynced[0] != simple;
                    ogg.SetField(OGG_UNSYNCED_LYRICS, simple);
                }
            }
            return changed;
        }

        private static bool IdenticalFrames(SynchronisedLyricsFrame frame1, SynchronisedLyricsFrame frame2)
        {
            return frame1.Render(4) == frame2.Render(4);
        }

        private static bool IdenticalFrames(UnsynchronisedLyricsFrame frame1, UnsynchronisedLyricsFrame frame2)
        {
            return frame1.Render(4) == frame2.Render(4);
        }

        public static Lyrics? FromFile(TagLib.File file)
        {
            return SharedIO.FromMany(new[] {
                SharedIO.MethodAttempt(() =>
                    (TagLib.Id3v2.Tag)file.GetTag(TagTypes.Id3v2),
                    x => FromId3v2(x)
                ),
                SharedIO.MethodAttempt(() =>
                    (TagLib.Ogg.XiphComment)file.GetTag(TagTypes.Xiph),
                    x => FromXiph(x)
                ),
                SharedIO.MethodAttempt(() =>
                    {
                        var lrc_file = Path.ChangeExtension(file.Name, ".lrc");
                        return File.Exists(lrc_file) ? File.ReadAllLines(lrc_file) : null;
                    },
                    x => FromLrc(x)
                ),
                SharedIO.MethodAttempt(() =>
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
            var text = tag.GetFirstField(OGG_LYRICS);
            if (text != null)
            {
                var lyrics = StringUtils.SplitLines(text).ToArray();
                if (TryFromLrc(lyrics, out var result))
                    return result;
                else
                    return new Lyrics(String.Join("\n", lyrics));
            }
            var unsynced = tag.GetFirstField(OGG_UNSYNCED_LYRICS);
            if (unsynced != null)
                return new Lyrics(String.Join("\n", unsynced));
            return null;
        }

        private static bool TryFromLrc(string[] lines, out Lyrics? result)
        {
            result = null;
            if (lines.Length > 0 && FromLrcLine(lines[0]) == null)
                return false;
            result = FromLrc(lines);
            return true;
        }

        private static LyricsEntry? FromLrcLine(string line)
        {
            var match = SharedIO.LrcRegex.Match(line);
            if (match.Success)
            {
                if (TimeSpan.TryParseExact(match.Groups["time"].Value, SharedIO.TimespanFormats, null, out var time))
                    return new LyricsEntry(match.Groups["line"].Value, time);
            }
            return null;
        }

        public static Lyrics? FromLrc(string[] lines)
        {
            var list = new List<LyricsEntry>();
            foreach (var line in lines)
            {
                var entry = FromLrcLine(line);
                if (entry != null)
                    list.Add(entry.Value);
            }
            if (list.Count == 0)
                return null;
            return new Lyrics(list);
        }
    }
}
