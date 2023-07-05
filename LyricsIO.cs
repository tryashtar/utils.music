using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TagLib;
using TagLib.Id3v2;
using TryashtarUtils.Utility;

namespace TryashtarUtils.Music
{
    public static class LyricsIO
    {
        public const string OGG_LYRICS = "LYRICS";
        public const string OGG_UNSYNCED_LYRICS = "UNSYNCED LYRICS";
        public const string RICH_LYRICS = "RICH LYRICS";

        public static bool ToFile(File file, Lyrics? lyrics, LyricTypes types)
        {
            bool changed = false;

            var id3v2 = (TagLib.Id3v2.Tag)file.GetTag(TagTypes.Id3v2);
            var ogg = (TagLib.Ogg.XiphComment)file.GetTag(TagTypes.Xiph);
            var ape = (TagLib.Ape.Tag)file.GetTag(TagTypes.Ape);

            if (id3v2 != null)
                changed |= ToId3v2(id3v2, lyrics, types);
            if (ape != null)
                changed |= ToApe(ape, lyrics, types);
            if (ogg != null)
                changed |= ToXiph(ogg, lyrics, types);

            return changed;
        }

        public static bool ToXiph(TagLib.Ogg.XiphComment tag, Lyrics? lyrics, LyricTypes types)
        {
            bool changed = false;
            if (types.HasFlag(LyricTypes.Synced))
            {
                if (lyrics == null)
                {
                    changed |= tag.GetFirstField(OGG_LYRICS) != null;
                    tag.RemoveField(OGG_LYRICS);
                }
                else
                {
                    string lrc = String.Join("\n", lyrics.ToLrc());
                    var existing = tag.GetField(OGG_LYRICS);
                    changed |= existing.Length != 1 || existing[0] != lrc;
                    tag.SetField(OGG_LYRICS, lrc);
                }
            }

            if (types.HasFlag(LyricTypes.Simple))
            {
                if (lyrics == null)
                {
                    changed |= tag.GetFirstField(OGG_UNSYNCED_LYRICS) != null;
                    tag.RemoveField(OGG_UNSYNCED_LYRICS);
                }
                else
                {
                    string simple = lyrics.ToSimple();
                    var existing_unsynced = tag.GetField(OGG_UNSYNCED_LYRICS);
                    changed |= existing_unsynced.Length != 1 || existing_unsynced[0] != simple;
                    tag.SetField(OGG_UNSYNCED_LYRICS, simple);
                }
            }

            if (types.HasFlag(LyricTypes.Rich))
            {
                if (lyrics == null)
                {
                    changed |= tag.GetFirstField(RICH_LYRICS) != null;
                    tag.RemoveField(RICH_LYRICS);
                }
                else
                {
                    string rich = JsonSerializer.Serialize(lyrics);
                    var existing_rich = tag.GetField(RICH_LYRICS);
                    changed |= existing_rich.Length != 1 || existing_rich[0] != rich;
                    tag.SetField(RICH_LYRICS, rich);
                }
            }

            return changed;
        }

        public static bool ToApe(TagLib.Ape.Tag tag, Lyrics? lyrics, LyricTypes types)
        {
            if (types.HasFlag(LyricTypes.Simple))
            {
                string? simple = lyrics?.ToSimple();
                bool changed = tag.Lyrics != simple;
                tag.Lyrics = simple;
                return changed;
            }

            return false;
        }

        public static bool ToId3v2(TagLib.Id3v2.Tag tag, Lyrics? lyrics, LyricTypes types)
        {
            bool changed = false;
            string language = LanguageExtensions.GetId3v2(tag) ?? "XXX";
            if (types.HasFlag(LyricTypes.Simple))
            {
                var unsynced_frames = tag.GetFrames<UnsynchronisedLyricsFrame>().ToList();
                foreach (var frame in unsynced_frames)
                {
                    tag.RemoveFrame(frame);
                }

                if (lyrics == null)
                    changed = changed || unsynced_frames.Count > 0;
                else
                {
                    var unsynced_frame = new UnsynchronisedLyricsFrame("", language, StringType.Latin1)
                    {
                        Text = lyrics.ToSimple()
                    };
                    tag.AddFrame(unsynced_frame);
                    changed = changed || unsynced_frames.Count != 1 ||
                              !IdenticalFrames(unsynced_frames[0], unsynced_frame);
                }
            }

            if (types.HasFlag(LyricTypes.Synced))
            {
                var synced_frames = tag.GetFrames<SynchronisedLyricsFrame>().ToList();
                foreach (var frame in synced_frames)
                {
                    tag.RemoveFrame(frame);
                }

                if (lyrics == null)
                    changed = changed || synced_frames.Count > 0;
                else
                {
                    var synced_frame =
                        new SynchronisedLyricsFrame("", language, SynchedTextType.Lyrics, StringType.Latin1)
                        {
                            Text = lyrics.ToSynchedText(),
                            Format = TimestampFormat.AbsoluteMilliseconds
                        };
                    tag.AddFrame(synced_frame);
                    changed = changed || synced_frames.Count != 1 || !IdenticalFrames(synced_frames[0], synced_frame);
                }
            }

            if (types.HasFlag(LyricTypes.Rich))
            {
                var rich_frames = tag.GetFrames<UserTextInformationFrame>().Where(x => x.Description == RICH_LYRICS)
                    .ToList();
                foreach (var frame in rich_frames)
                {
                    tag.RemoveFrame(frame);
                }

                if (lyrics == null)
                    changed = changed || rich_frames.Count > 0;
                else
                {
                    var rich_frame = new UserTextInformationFrame(RICH_LYRICS, StringType.Latin1)
                    {
                        Text = new[] { JsonSerializer.Serialize(lyrics) }
                    };
                    tag.AddFrame(rich_frame);
                    changed = changed || rich_frames.Count != 1 || !IdenticalFrames(rich_frames[0], rich_frame);
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

        private static bool IdenticalFrames(TextInformationFrame frame1, TextInformationFrame frame2)
        {
            return frame1.Render(4) == frame2.Render(4);
        }

        public static Lyrics? FromFile(File file, LyricTypes type)
        {
            return SharedIO.FromMany(new[]
            {
                SharedIO.MethodAttempt(() =>
                        (TagLib.Id3v2.Tag)file.GetTag(TagTypes.Id3v2),
                    x => FromId3v2(x, file.Properties.Duration, type)
                ),
                SharedIO.MethodAttempt(() =>
                        (TagLib.Ogg.XiphComment)file.GetTag(TagTypes.Xiph),
                    x => FromXiph(x, file.Properties.Duration, type)
                )
            });
        }

        public static Lyrics? FromId3v2(TagLib.Id3v2.Tag tag, TimeSpan duration, LyricTypes type)
        {
            if (type.HasFlag(LyricTypes.Rich))
            {
                foreach (var frame in tag.GetFrames<UserTextInformationFrame>()
                             .Where(x => x.Description == RICH_LYRICS))
                {
                    if (frame.Text.Length > 0)
                        return JsonSerializer.Deserialize<Lyrics>(frame.Text[0]);
                }
            }

            if (type.HasFlag(LyricTypes.Synced))
            {
                foreach (var frame in tag.GetFrames<SynchronisedLyricsFrame>())
                {
                    return new Lyrics(frame.Text, duration);
                }
            }

            if (type.HasFlag(LyricTypes.Simple) && !String.IsNullOrEmpty(tag.Lyrics))
                return new Lyrics(tag.Lyrics);

            return null;
        }

        public static Lyrics? FromXiph(TagLib.Ogg.XiphComment tag, TimeSpan duration, LyricTypes type)
        {
            if (type.HasFlag(LyricTypes.Rich))
            {
                var rich = tag.GetFirstField(RICH_LYRICS);
                if (rich != null)
                    return JsonSerializer.Deserialize<Lyrics>(rich);
            }
            
            var text = tag.GetFirstField(OGG_LYRICS);
            if (text != null)
            {
                var lyrics = StringUtils.SplitLines(text).ToArray();
                if (type.HasFlag(LyricTypes.Synced) && TryFromLrc(lyrics, duration, out var result))
                    return result;
                else if (type.HasFlag(LyricTypes.Simple))
                    return new Lyrics(String.Join('\n', lyrics));
            }

            if (type.HasFlag(LyricTypes.Simple))
            {
                var unsynced = tag.GetFirstField(OGG_UNSYNCED_LYRICS);
                if (unsynced != null)
                    return new Lyrics(String.Join('\n', unsynced));
            }

            return null;
        }

        private static SynchedText? ParseSynchedText(string line)
        {
            var match = SharedIO.LrcRegex.Match(line);
            if (match.Success)
            {
                if (TimeSpan.TryParseExact(match.Groups["time"].Value, SharedIO.TimespanFormats, null, out var time))
                    return new SynchedText((long)time.TotalMilliseconds, match.Groups["line"].Value);
            }

            return null;
        }

        public static bool TryFromLrc(string[] lines, TimeSpan duration, out Lyrics? result)
        {
            result = null;
            if (lines.Length > 0 && ParseSynchedText(lines[0]) == null)
                return false;
            result = FromLrc(lines, duration);
            return true;
        }

        public static Lyrics FromLrc(IEnumerable<string> lines, TimeSpan? duration = null)
        {
            var results = new List<SynchedText>();
            foreach (var line in lines)
            {
                var text = ParseSynchedText(line);
                if (text != null)
                    results.Add(text.Value);
            }

            return new Lyrics(results, duration);
        }
    }

    [Flags]
    public enum LyricTypes
    {
        Simple = 1,
        Synced = 2,
        Rich = 4
    }
}