using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    public static class ChaptersIO
    {
        public const uint MAX_OGG_CHAPTERS = 999;
        public const string OGG_CHAPTER_NAME = "NAME";
        public static string ChapterTimeKey(uint num)
        {
            if (num > MAX_OGG_CHAPTERS)
                throw new ArgumentOutOfRangeException(nameof(num));
            return "CHAPTER" + num.ToString("000");
        }

        private class ChapterFrameComparer : IEqualityComparer<ChapterFrame>
        {
            public static readonly ChapterFrameComparer Instance = new();
            private ChapterFrameComparer() { }
            public bool Equals(ChapterFrame? x, ChapterFrame? y)
            {
                if (x == null)
                    return y == null;
                if (y == null)
                    return x == null;
                return x.Render(4) == y.Render(4);
            }

            public int GetHashCode(ChapterFrame obj)
            {
                return obj.Render(4).GetHashCode();
            }
        }

        public static bool ToFile(TagLib.File file, ChapterCollection? chapters)
        {
            bool changed = false;
            var id3v2 = (TagLib.Id3v2.Tag)file.GetTag(TagTypes.Id3v2);
            var ogg = (TagLib.Ogg.XiphComment)file.GetTag(TagTypes.Xiph);

            if (id3v2 != null)
            {
                var existing_frames = id3v2.GetFrames<ChapterFrame>().ToList();
                foreach (var frame in existing_frames)
                {
                    id3v2.RemoveFrame(frame);
                }
                if (chapters != null)
                {
                    var new_frames = new List<ChapterFrame>();
                    for (int i = 0; i < chapters.Chapters.Count; i++)
                    {
                        var chapter = chapters.Chapters[i];
                        var new_frame = new ChapterFrame(ChapterTimeKey(chapter.Number), chapter.Title)
                        {
                            StartMilliseconds = (uint)chapter.Time.TotalMilliseconds,
                            EndMilliseconds = (uint)(i >= chapters.Chapters.Count - 1 ? file.Properties.Duration : chapters.Chapters[i + 1].Time).TotalMilliseconds
                        };
                        id3v2.AddFrame(new_frame);
                        new_frames.Add(new_frame);
                    }
                    existing_frames.Sort((x, y) => x.Id.CompareTo(y.Id));
                    new_frames.Sort((x, y) => x.Id.CompareTo(y.Id));
                    changed = changed || !new_frames.SequenceEqual(existing_frames, ChapterFrameComparer.Instance);
                }
                else
                    changed |= existing_frames.Count > 0;
            }
            if (ogg != null)
            {
                var writing = new Dictionary<uint, Chapter>();
                if (chapters != null)
                {
                    foreach (var chapter in chapters.Chapters)
                    {
                        writing[chapter.Number] = chapter;
                    }
                }
                for (uint i = 0; i < MAX_OGG_CHAPTERS; i++)
                {
                    string chapter_num = ChapterTimeKey(i);
                    string? desired_time = null;
                    string? desired_name = null;
                    if (writing.TryGetValue(i, out var chapter))
                    {
                        desired_time = StringUtils.TimeSpan(chapter.Time);
                        desired_name = chapter.Title;
                    }
                    var current_time = ogg.GetField(chapter_num);
                    var current_name = ogg.GetField(chapter_num + OGG_CHAPTER_NAME);
                    ogg.SetField(chapter_num, desired_time);
                    ogg.SetField(chapter_num + OGG_CHAPTER_NAME, desired_name);
                    changed |= current_time.Length > 1 || (current_time.Length == 0) != (desired_time == null);
                    changed |= current_name.Length > 1 || (current_name.Length == 0) != (desired_name == null);
                }
            }
            return changed;
        }

        public static ChapterCollection? FromFile(TagLib.File file)
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
                        var chp_file = Path.ChangeExtension(file.Name, ".chp");
                        return File.Exists(chp_file) ? File.ReadAllLines(chp_file) : null;
                    },
                    x => FromChp(x)
                )
            });
        }

        public static ChapterCollection? FromId3v2(TagLib.Id3v2.Tag tag)
        {
            uint num = 1;
            var chapters = new List<Chapter>();
            foreach (var frame in tag.GetFrames<ChapterFrame>())
            {
                var chapter = ChapterFromFrame(frame, num);
                if (chapter.HasValue)
                {
                    chapters.Add(chapter.Value);
                    num++;
                }
            }
            if (chapters.Count == 0)
                return null;
            return new ChapterCollection(chapters);
        }

        public static Chapter? ChapterFromFrame(ChapterFrame frame, uint num)
        {
            var title = frame.SubFrames.OfType<TextInformationFrame>().FirstOrDefault();
            if (title == null || title.Text == null || title.Text.Length == 0)
                return null;
            return new Chapter(num, title.Text[0], TimeSpan.FromMilliseconds(frame.StartMilliseconds));
        }

        public static ChapterCollection? FromXiph(TagLib.Ogg.XiphComment tag)
        {
            var chapters = new List<Chapter>();
            for (uint i = 0; i < MAX_OGG_CHAPTERS; i++)
            {
                string chapter_num = ChapterTimeKey(i);
                string time = tag.GetFirstField(chapter_num);
                if (time != null)
                {
                    var time_real = TimeSpan.ParseExact(time, SharedIO.TimespanFormats, null);
                    string title = tag.GetFirstField(chapter_num + OGG_CHAPTER_NAME);
                    title ??= "Chapter " + i;
                    chapters.Add(new Chapter(i, title, time_real));
                }
            }
            if (chapters.Count == 0)
                return null;
            return new ChapterCollection(chapters);
        }

        public static ChapterCollection? FromChp(string[] lines)
        {
            var chapters = new List<Chapter>();
            uint num = 1;
            foreach (var line in lines)
            {
                var match = SharedIO.LrcRegex.Match(line);
                if (match.Success)
                {
                    if (TimeSpan.TryParseExact(match.Groups["time"].Value, SharedIO.TimespanFormats, null, out var time))
                        chapters.Add(new Chapter(num, match.Groups["line"].Value, time));
                    num++;
                }
            }
            if (chapters.Count == 0)
                return null;
            return new ChapterCollection(chapters);
        }
    }
}
