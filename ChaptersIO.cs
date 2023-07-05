using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TagLib;
using TagLib.Id3v2;
using TryashtarUtils.Utility;

namespace TryashtarUtils.Music
{
    public static class ChaptersIO
    {
        public const uint MAX_OGG_CHAPTERS = 999;
        public const string OGG_CHAPTER_NAME = "NAME";
        public const string RICH_CHAPTERS = "RICH CHAPTERS";

        public static string ChapterTimeKey(uint num)
        {
            if (num > MAX_OGG_CHAPTERS)
                throw new ArgumentOutOfRangeException(nameof(num));
            return "CHAPTER" + num.ToString("000");
        }

        private class ChapterFrameComparer : IEqualityComparer<ChapterFrame>
        {
            public static readonly ChapterFrameComparer Instance = new();

            private ChapterFrameComparer()
            {
            }

            public bool Equals(ChapterFrame? x, ChapterFrame? y)
            {
                if (x == null)
                    return y == null;
                if (y == null)
                    return false;
                return x.Render(4) == y.Render(4);
            }

            public int GetHashCode(ChapterFrame obj)
            {
                return obj.Render(4).GetHashCode();
            }
        }

        public static bool ToFile(File file, ChapterCollection? chapters, ChapterTypes types)
        {
            bool changed = false;
            var id3v2 = (TagLib.Id3v2.Tag)file.GetTag(TagTypes.Id3v2);
            var ogg = (TagLib.Ogg.XiphComment)file.GetTag(TagTypes.Xiph);

            if (id3v2 != null)
                changed |= ToId3v2(id3v2, chapters, types);
            if (ogg != null)
                changed |= ToXiph(ogg, chapters, types);

            return changed;
        }

        public static ChapterCollection? FromFile(File file, ChapterTypes type)
        {
            return SharedIO.FromMany(new[]
            {
                SharedIO.MethodAttempt(() =>
                        (TagLib.Id3v2.Tag)file.GetTag(TagTypes.Id3v2),
                    x => FromId3v2(x, type)
                ),
                SharedIO.MethodAttempt(() =>
                        (TagLib.Ogg.XiphComment)file.GetTag(TagTypes.Xiph),
                    x => FromXiph(x, file.Properties.Duration, type)
                )
            });
        }

        public static ChapterCollection? FromId3v2(TagLib.Id3v2.Tag tag, ChapterTypes type)
        {
            if (type.HasFlag(ChapterTypes.Rich))
            {
                foreach (var frame in tag.GetFrames<UserTextInformationFrame>()
                             .Where(x => x.Description == RICH_CHAPTERS))
                {
                    if (frame.Text.Length > 0)
                        return JsonSerializer.Deserialize<ChapterCollection>(frame.Text[0]);
                }
            }

            if (type.HasFlag(ChapterTypes.Simple))
            {
                var chapters = new List<Chapter>();
                foreach (var frame in tag.GetFrames<ChapterFrame>())
                {
                    var chapter = ChapterFromFrame(frame);
                    if (chapter != null)
                        chapters.Add(chapter);
                }

                if (chapters.Count != 0)
                    return new ChapterCollection(chapters);
            }

            return null;
        }

        public static bool ToId3v2(TagLib.Id3v2.Tag tag, ChapterCollection? chapters, ChapterTypes types)
        {
            bool changed = false;
            if (types.HasFlag(ChapterTypes.Rich))
            {
                var rich_frames = tag.GetFrames<UserTextInformationFrame>().Where(x => x.Description == RICH_CHAPTERS)
                    .ToList();
                foreach (var frame in rich_frames)
                {
                    tag.RemoveFrame(frame);
                }

                if (chapters == null)
                    changed = changed || rich_frames.Count > 0;
                else
                {
                    var rich_frame = new UserTextInformationFrame(RICH_CHAPTERS, StringType.Latin1)
                    {
                        Text = new[] { JsonSerializer.Serialize(chapters, LyricsIO.SerializeOptions) }
                    };
                    tag.AddFrame(rich_frame);
                    changed = changed || rich_frames.Count == 0 || !IdenticalFrames(rich_frames[0], rich_frame);
                }
            }

            if (types.HasFlag(ChapterTypes.Simple))
            {
                var simple_frames = tag.GetFrames<ChapterFrame>().ToList();
                foreach (var frame in simple_frames)
                {
                    tag.RemoveFrame(frame);
                }

                if (chapters == null)
                    changed = changed || simple_frames.Count > 0;
                else
                {
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

                    simple_frames.Sort((x, y) => String.CompareOrdinal(x.Id, y.Id));
                    new_frames.Sort((x, y) => String.CompareOrdinal(x.Id, y.Id));
                    changed = changed || !new_frames.SequenceEqual(simple_frames, ChapterFrameComparer.Instance);
                }
            }

            return changed;
        }

        private static bool IdenticalFrames(TextInformationFrame frame1, TextInformationFrame frame2)
        {
            return frame1.Render(4) == frame2.Render(4);
        }

        public static Chapter? ChapterFromFrame(ChapterFrame frame)
        {
            var title = frame.SubFrames.OfType<TextInformationFrame>().FirstOrDefault();
            if (title == null || title.Text == null || title.Text.Length == 0)
                return null;
            return new Chapter(title.Text[0], TimeSpan.FromMilliseconds(frame.StartMilliseconds),
                TimeSpan.FromMilliseconds(frame.EndMilliseconds));
        }

        public static ChapterCollection? FromXiph(TagLib.Ogg.XiphComment tag, TimeSpan duration, ChapterTypes type)
        {
            if (type.HasFlag(ChapterTypes.Rich))
            {
                var rich = tag.GetFirstField(RICH_CHAPTERS);
                if (rich != null)
                    return JsonSerializer.Deserialize<ChapterCollection>(rich);
            }

            if (type.HasFlag(ChapterTypes.Simple))
            {
                var chapters = new List<Chapter>();
                Action<TimeSpan> add_previous_chapter = _ => { };
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
                if (chapters.Count > 0)
                    return new ChapterCollection(chapters);
            }

            return null;
        }

        public static bool ToXiph(TagLib.Ogg.XiphComment tag, ChapterCollection? chapters, ChapterTypes types)
        {
            bool changed = false;
            if (types.HasFlag(ChapterTypes.Simple))
            {
                for (int i = 0; i < MAX_OGG_CHAPTERS; i++)
                {
                    string chapter_num = ChapterTimeKey((uint)i);
                    string? desired_time = null;
                    string? desired_name = null;
                    if (chapters != null && i < chapters.Chapters.Count)
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
            }

            if (types.HasFlag(ChapterTypes.Rich))
            {
                var existing_rich = tag.GetField(RICH_CHAPTERS);
                if (chapters == null)
                {
                    tag.RemoveField(RICH_CHAPTERS);
                    changed = changed || existing_rich.Length > 0;
                }
                else
                {
                    string rich = JsonSerializer.Serialize(chapters, LyricsIO.SerializeOptions);
                    changed |= existing_rich.Length != 1 || existing_rich[0] != rich;
                    tag.SetField(RICH_CHAPTERS, rich);
                }
            }

            return changed;
        }

        public static ChapterCollection FromChp(IEnumerable<string> lines, TimeSpan? duration = null)
        {
            var chapters = new List<Chapter>();
            Action<TimeSpan> add_previous_chapter = _ => { };
            foreach (var line in lines)
            {
                var match = SharedIO.LrcRegex.Match(line);
                if (match.Success)
                {
                    if (TimeSpan.TryParseExact(match.Groups["time"].Value, SharedIO.TimespanFormats, null,
                            out var time))
                    {
                        string text = match.Groups["line"].Value;
                        add_previous_chapter(time);
                        duration ??= time;
                        add_previous_chapter = x => { chapters.Add(new Chapter(text, time, x)); };
                    }
                }
            }

            if (duration != null)
                add_previous_chapter(duration.Value);
            return new ChapterCollection(chapters);
        }
    }

    [Flags]
    public enum ChapterTypes
    {
        Simple = 1,
        Rich = 2
    }
}