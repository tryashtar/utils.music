﻿using System;
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
        public const string OGG_LYRICS = "SYNCED LYRICS";
        public static bool ToFile(TagLib.File file, Lyrics? lyrics)
        {
            bool changed = false;
            var simple = lyrics?.ToSimple();
            changed |= file.Tag.Lyrics != simple;
            file.Tag.Lyrics = simple;

            var id3v2 = (TagLib.Id3v2.Tag)file.GetTag(TagTypes.Id3v2);
            var ogg = (TagLib.Ogg.XiphComment)file.GetTag(TagTypes.Xiph);

            if (id3v2 != null)
            {
                var existing_frames = id3v2.GetFrames<SynchronisedLyricsFrame>().ToList();
                foreach (var frame in existing_frames)
                {
                    id3v2.RemoveFrame(frame);
                }
                changed |= existing_frames.Count > 1;
                if (lyrics != null)
                {
                    string? language = Language.Get(id3v2) ?? "XXX";
                    var new_frame = new SynchronisedLyricsFrame("", language, SynchedTextType.Lyrics, StringType.Latin1)
                    {
                        Text = lyrics.ToSynchedText(),
                        Format = TimestampFormat.AbsoluteMilliseconds
                    };
                    id3v2.AddFrame(new_frame);
                    // short circuit
                    changed = changed || existing_frames.Count == 0 || !IdenticalFrames(existing_frames[0], new_frame);
                }
            }
            if (ogg != null)
            {
                if (lyrics == null)
                {
                    changed |= ogg.GetFirstField(OGG_LYRICS) != null;
                    ogg.RemoveField(OGG_LYRICS);
                }
                else
                {
                    var lrc = lyrics.ToLrc();
                    var existing = ogg.GetField(OGG_LYRICS);
                    changed |= !existing.SequenceEqual(lrc);
                    ogg.SetField(OGG_LYRICS, lrc);

                }
            }
            return changed;
        }

        private static bool IdenticalFrames(SynchronisedLyricsFrame frame1, SynchronisedLyricsFrame frame2)
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
            var lyrics = tag.GetField(OGG_LYRICS);
            if (lyrics != null && lyrics.Length > 0)
                return FromLrc(lyrics);
            return null;
        }

        public static Lyrics? FromLrc(string[] lines)
        {
            var list = new List<LyricsEntry>();
            foreach (var line in lines)
            {
                var match = SharedIO.LrcRegex.Match(line);
                if (match.Success)
                {
                    if (TimeSpan.TryParseExact(match.Groups["time"].Value, SharedIO.TimespanFormats, null, out var time))
                        list.Add(new LyricsEntry(match.Groups["line"].Value, time));
                }
            }
            if (list.Count == 0)
                return null;
            return new Lyrics(list);
        }
    }
}
