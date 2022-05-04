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

        public ChapterCollection(SynchedText[] text)
        {
            uint num = 1;
            foreach (var item in text)
            {
                Entries.Add(new Chapter(num, item.Text, TimeSpan.FromMilliseconds(item.Time)));
                num++;
            }
        }

        public ChapterCollection(IEnumerable<Chapter> entries)
        {
            foreach (var item in entries)
            {
                Entries.Add(item);
            }
        }

        private static readonly IComparer<Chapter> TimeComparer = new LambdaComparer<Chapter, TimeSpan>(x => x.Time);

        public Chapter? ChapterAtTime(TimeSpan time)
        {
            var fake = new Chapter(0, "", time);
            int search = Entries.BinarySearch(fake, TimeComparer);
            if (search < 0)
            {
                search = ~search;
                search--;
            }
            if (search < 0)
                return null;
            search = Math.Min(search, Entries.Count - 1);
            return Entries[search];
        }

        public SynchedText[] ToSynchedText()
        {
            return Entries.Select(x => new SynchedText((long)x.Time.TotalMilliseconds, x.Title)).ToArray();
        }

        public string[] ToChp()
        {
            return Entries.Select(x => x.ToChpEntry()).ToArray();
        }
    }

    public record Chapter(uint Number, string Title, TimeSpan Time)
    {
        public string ToChpEntry()
        {
            string time_str = StringUtils.TimeSpan(Time);
            return $"[{time_str}]{Title}";
        }
    }
}
