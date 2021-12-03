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

        public static void ToFile(TagLib.File file, ChapterCollection? chapters)
        {
            var id3v2 = (TagLib.Id3v2.Tag)file.GetTag(TagTypes.Id3v2);
            var ogg = (TagLib.Ogg.XiphComment)file.GetTag(TagTypes.Xiph);

            if (id3v2 != null)
            {
                foreach (var frame in id3v2.GetFrames<ChapterFrame>().ToList())
                {
                    id3v2.RemoveFrame(frame);
                }
                if (chapters != null)
                {
                    foreach (var chapter in chapters.Chapters)
                    {
                        var new_frame = new ChapterFrame(ChapterTimeKey(chapter.Number), chapter.Title)
                        {
                            StartMilliseconds = (uint)chapter.Time.TotalMilliseconds
                        };
                        id3v2.AddFrame(new_frame);
                    }
                }
            }
            if (ogg != null)
            {
                for (uint i = 0; i < MAX_OGG_CHAPTERS; i++)
                {
                    string chapter_num = ChapterTimeKey(i);
                    ogg.RemoveField(chapter_num);
                    ogg.RemoveField(chapter_num + OGG_CHAPTER_NAME);
                }
                if (chapters != null)
                {
                    foreach (var chapter in chapters.Chapters)
                    {
                        string chapter_num = ChapterTimeKey(chapter.Number);
                        ogg.SetField(chapter_num, new[] { StringUtils.TimeSpan(chapter.Time) });
                        ogg.SetField(chapter_num + OGG_CHAPTER_NAME, new[] { chapter.Title });
                    }
                }
            }
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
