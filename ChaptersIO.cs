﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TagLib;
using TagLib.Id3v2;
using TryashtarUtils.Utility;
using File = System.IO.File;

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

        public static bool ToFile(TagLib.File file, ChapterCollection chapters)
        {
            bool changed = false;
            var id3v2 = (TagLib.Id3v2.Tag)file.GetTag(TagTypes.Id3v2);
            var ogg = (TagLib.Ogg.XiphComment)file.GetTag(TagTypes.Xiph);

            if (id3v2 != null)
                changed |= ToId3v2(id3v2, chapters);
            if (ogg != null)
                changed |= ToXiph(ogg, chapters);

            return changed;
        }

        public static ChapterCollection? FromFile(TagLib.File file)
        {
            return SharedIO.FromMany(new[] {
                SharedIO.MethodAttempt(() =>
                    (TagLib.Id3v2.Tag)file.GetTag(TagTypes.Id3v2),
                    x => FromId3v2(x, file.Properties.Duration)
                ),
                SharedIO.MethodAttempt(() =>
                    (TagLib.Ogg.XiphComment)file.GetTag(TagTypes.Xiph),
                    x => FromXiph(x, file.Properties.Duration)
                ),
                SharedIO.MethodAttempt(() =>
                    {
                        var chp_file = Path.ChangeExtension(file.Name, ".chp");
                        return File.Exists(chp_file) ? File.ReadLines(chp_file) : null;
                    },
                    x => FromChp(x, file.Properties.Duration)
                )
            });
        }

        public static ChapterCollection FromId3v2(TagLib.Id3v2.Tag tag, TimeSpan duration)
        {
            var chapters = new List<Chapter>();
            foreach (var frame in tag.GetFrames<ChapterFrame>())
            {
                var chapter = ChapterFromFrame(frame);
                if (chapter != null)
                    chapters.Add(chapter);
            }
            return new ChapterCollection(chapters);
        }

        public static bool ToId3v2(TagLib.Id3v2.Tag tag, ChapterCollection chapters)
        {
            var existing_frames = tag.GetFrames<ChapterFrame>().ToList();
            foreach (var frame in existing_frames)
            {
                tag.RemoveFrame(frame);
            }
            var new_frames = new List<ChapterFrame>();
            for (int i = 0; i < chapters.Chapters.Count; i++)
            {
                var chapter = chapters.Chapters[i];
                var new_frame = new ChapterFrame(ChapterTimeKey((uint)i), chapter.Title)
                {
                    StartMilliseconds = (uint)chapter.Start.TotalMilliseconds,
                    EndMilliseconds = (uint)chapter.End.TotalMilliseconds
                };
                tag.AddFrame(new_frame);
                new_frames.Add(new_frame);
            }
            existing_frames.Sort((x, y) => x.Id.CompareTo(y.Id));
            new_frames.Sort((x, y) => x.Id.CompareTo(y.Id));
            return !new_frames.SequenceEqual(existing_frames, ChapterFrameComparer.Instance);
        }

        public static Chapter? ChapterFromFrame(ChapterFrame frame)
        {
            var title = frame.SubFrames.OfType<TextInformationFrame>().FirstOrDefault();
            if (title == null || title.Text == null || title.Text.Length == 0)
                return null;
            return new Chapter(title.Text[0], TimeSpan.FromMilliseconds(frame.StartMilliseconds), TimeSpan.FromMilliseconds(frame.EndMilliseconds));
        }

        public static ChapterCollection FromXiph(TagLib.Ogg.XiphComment tag, TimeSpan duration)
        {
            var chapters = new List<Chapter>();
            Action<TimeSpan> add_previous_chapter = x => { };
            for (uint i = 0; i < MAX_OGG_CHAPTERS; i++)
            {
                string chapter_num = ChapterTimeKey(i);
                string time = tag.GetFirstField(chapter_num);
                if (time != null)
                {
                    var time_real = TimeSpan.ParseExact(time, SharedIO.TimespanFormats, null);
                    string title = tag.GetFirstField(chapter_num + OGG_CHAPTER_NAME) ?? "Chapter " + (i + 1);
                    add_previous_chapter(time_real);
                    add_previous_chapter = x => { chapters.Add(new Chapter(title, time_real, x)); };
                }
            }
            add_previous_chapter(duration);
            return new ChapterCollection(chapters);
        }

        public static bool ToXiph(TagLib.Ogg.XiphComment tag, ChapterCollection chapters)
        {
            bool changed = false;
            for (int i = 0; i < MAX_OGG_CHAPTERS; i++)
            {
                string chapter_num = ChapterTimeKey((uint)i);
                string? desired_time = null;
                string? desired_name = null;
                if (i < chapters.Chapters.Count)
                {
                    desired_time = StringUtils.TimeSpan(chapters.Chapters[i].Start);
                    desired_name = chapters.Chapters[i].Title;
                }
                var current_time = tag.GetField(chapter_num);
                var current_name = tag.GetField(chapter_num + OGG_CHAPTER_NAME);
                tag.SetField(chapter_num, desired_time);
                tag.SetField(chapter_num + OGG_CHAPTER_NAME, desired_name);
                changed |= current_time.Length > 1 || (current_time.Length == 0) != (desired_time == null);
                changed |= current_name.Length > 1 || (current_name.Length == 0) != (desired_name == null);
            }
            return changed;
        }

        public static ChapterCollection FromChp(IEnumerable<string> lines, TimeSpan? duration = null)
        {
            var chapters = new List<Chapter>();
            Action<TimeSpan> add_previous_chapter = x => { };
            foreach (var line in lines)
            {
                var match = SharedIO.LrcRegex.Match(line);
                if (match.Success)
                {
                    if (TimeSpan.TryParseExact(match.Groups["time"].Value, SharedIO.TimespanFormats, null, out var time))
                    {
                        string text = match.Groups["line"].Value;
                        add_previous_chapter(time);
                        if (duration == null)
                            duration = time;
                        add_previous_chapter = x => { chapters.Add(new Chapter(text, time, x)); };
                    }
                }
            }
            if (duration != null)
                add_previous_chapter(duration.Value);
            return new ChapterCollection(chapters);
        }
    }
}
