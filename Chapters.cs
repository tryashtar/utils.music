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
    public class ChapterCollection
    {
        private readonly List<Chapter> Entries = new();
        public ReadOnlyCollection<Chapter> Chapters => Entries.AsReadOnly();

        public ChapterCollection(IEnumerable<Chapter> entries)
        {
            foreach (var item in entries)
            {
                Entries.Add(item);
            }
        }

        public string ToChp()
        {
            return String.Join(Environment.NewLine, Entries.Select(x => x.ToChpEntry()));
        }
    }

    public struct Chapter
    {
        public readonly uint Number;
        public readonly string Title;
        public readonly TimeSpan Time;
        public Chapter(uint number, string title, TimeSpan time)
        {
            Number = number;
            Title = title;
            Time = time;
        }

        public string ToChpEntry()
        {
            string time_str = StringUtils.TimeSpan(Time);
            return $"[{time_str}]{Title}";
        }
    }
}
