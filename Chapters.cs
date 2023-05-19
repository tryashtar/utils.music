using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using TryashtarUtils.Utility;

namespace TryashtarUtils.Music
{
    public class ChapterCollection : INotifyPropertyChanged
    {
        [JsonInclude]
        [JsonPropertyName("chapters")]
        // should be private, except JSON requires public
        public List<Chapter> Entries { get; private set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        // always presents results in order
        // previously used SortedList, but that doesn't work with mutable chapters
        [JsonIgnore]
        public ReadOnlyCollection<Chapter> Chapters => new(Entries.OrderBy(x => x, ChapterComparer.Instance).ToList());

        public ChapterCollection(IEnumerable<Chapter> entries)
        {
            Entries.AddRange(entries);
        }

        public ChapterCollection()
        {
        }

        public void Add(Chapter chapter)
        {
            Entries.Add(chapter);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Chapters)));
        }

        public bool Remove(Chapter chapter)
        {
            if (Entries.Remove(chapter))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Chapters)));
                return true;
            }

            return false;
        }

        public void Clear()
        {
            if (Entries.Count > 0)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Chapters)));
            Entries.Clear();
        }

        public IEnumerable<Chapter> ChaptersAtTime(TimeSpan time)
        {
            // use "Chapters" not "Entries" so results appear in order
            foreach (var chapter in Chapters)
            {
                if (time >= chapter.Start && time <= chapter.End)
                    yield return chapter;
            }
        }

        public IEnumerable<TimeSpan> UniqueSegments()
        {
            return Entries.Select(x => x.Start).Concat(Entries.Select(x => x.End)).Distinct().OrderBy(x => x);
        }

        public string[] ToChp()
        {
            return Chapters.Select(x => x.ToChpEntry()).ToArray();
        }

        public override string ToString()
        {
            return String.Join("\n", Chapters);
        }
    }

    public class Chapter : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private string title;

        [JsonPropertyName("title")]
        public string Title
        {
            get => title;
            set
            {
                title = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
            }
        }

        private TimeSpan start;

        [JsonPropertyName("start")]
        public TimeSpan Start
        {
            get => start;
            set
            {
                start = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Start)));
            }
        }

        private TimeSpan end;

        [JsonPropertyName("end")]
        public TimeSpan End
        {
            get => end;
            set
            {
                end = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(End)));
            }
        }
        
        public Chapter(string title, TimeSpan start, TimeSpan end)
        {
            this.title = title;
            this.start = start;
            this.end = end;
        }

        public string ToChpEntry()
        {
            string time_str = StringUtils.TimeSpan(Start);
            return $"[{time_str}]{Title}";
        }

        public override string ToString()
        {
            return $"[{Start}]-[{End}]: {Title}";
        }
    }

    public class ChapterComparer : IComparer<Chapter>
    {
        public static readonly ChapterComparer Instance = new();

        public int Compare(Chapter? x, Chapter? y)
        {
            if (x == null && y == null)
                return 0;
            else if (x == null)
                return -1;
            else if (y == null)
                return 1;
            int start = x.Start.CompareTo(y.Start);
            if (start != 0)
                return start;
            int end = x.End.CompareTo(y.End);
            if (end != 0)
                return end;
            return String.CompareOrdinal(x.Title, y.Title);
        }
    }
}