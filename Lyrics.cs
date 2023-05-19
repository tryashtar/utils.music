using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using TagLib.Id3v2;
using TryashtarUtils.Utility;

namespace TryashtarUtils.Music
{
    public class Lyrics : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        [JsonIgnore] public bool Synchronized { get; }

        [JsonInclude]
        [JsonPropertyName("channels")]
        // should be private, except JSON requires public
        public List<LyricsChannel> ChannelList { get; private set; } = new();

        [JsonIgnore]
        public ReadOnlyCollection<LyricsChannel> Channels =>
            new(ChannelList.OrderBy(x => x, ChannelComparer.Instance).ToList());

        // use "ChannelList" instead of "Channels" since we are sorting lyrics anyway, don't need sorted channels
        // this way, lyrics will show up in time order across channels, instead of all of one channel, then all of another
        [JsonIgnore]
        public IEnumerable<LyricsEntry> AllLyrics =>
            ChannelList.SelectMany(x => x.Lyrics).OrderBy(x => x, LyricsComparer.Instance);

        [JsonConstructor]
        public Lyrics(bool synchronized = true)
        {
            Synchronized = synchronized;
        }

        public Lyrics(IEnumerable<SynchedText> text, TimeSpan? duration = null)
        {
            Synchronized = true;
            var channel = new LyricsChannel();
            ChannelList.Add(channel);
            var list = text.ToArray();
            for (int i = 0; i < list.Length; i++)
            {
                var start = TimeSpan.FromMilliseconds(list[i].Time);
                TimeSpan end = start;
                if (i < list.Length - 1)
                    end = TimeSpan.FromMilliseconds(list[i + 1].Time);
                else if (duration != null)
                    end = duration.Value;
                channel.Add(new LyricsEntry(list[i].Text, start, end));
            }
        }

        public Lyrics(IEnumerable<LyricsEntry> entries)
        {
            Synchronized = true;
            var channel = new LyricsChannel();
            ChannelList.Add(channel);
            foreach (var item in entries)
            {
                channel.Add(item);
            }
        }

        public Lyrics(string lyrics)
        {
            Synchronized = false;
            var channel = new LyricsChannel();
            ChannelList.Add(channel);
            foreach (var line in StringUtils.SplitLines(lyrics))
            {
                channel.Add(new LyricsEntry(line, TimeSpan.Zero, TimeSpan.Zero));
            }
        }

        public void AddChannel(LyricsChannel channel)
        {
            ChannelList.Add(channel);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Channels)));
        }

        public bool RemoveChannel(LyricsChannel channel)
        {
            if (ChannelList.Remove(channel))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Channels)));
                return true;
            }

            return false;
        }

        public void ClearChannels()
        {
            if (ChannelList.Count > 0)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Channels)));
            ChannelList.Clear();
        }

        public IEnumerable<LyricsEntry> LyricsAtTime(TimeSpan time)
        {
            if (!Synchronized)
                yield break;
            foreach (var channel in Channels)
            {
                foreach (var line in channel.Lyrics)
                {
                    if (time >= line.Start && time <= line.End)
                        yield return line;
                }
            }
        }

        public SynchedText[] ToSynchedText()
        {
            return AllLyrics.Select(x => new SynchedText((long)x.Start.TotalMilliseconds, x.Text)).ToArray();
        }

        public string ToSimple()
        {
            return String.Join("\n", AllLyrics.Select(x => x.Text));
        }

        public string[] ToLrc()
        {
            return AllLyrics.Select(x => x.ToLrcEntry()).ToArray();
        }

        public override string ToString()
        {
            return String.Join("\n", AllLyrics);
        }
    }

    public class LyricsChannel : INotifyPropertyChanged
    {
        private string? name;

        [JsonPropertyName("name")]
        public string? Name
        {
            get => name;
            set
            {
                name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        [JsonIgnore] public TimeSpan? Start => Entries.Count == 0 ? null : Entries.Min(x => x.Start);
        [JsonIgnore] public TimeSpan? End => Entries.Count == 0 ? null : Entries.Max(x => x.End);

        public event PropertyChangedEventHandler? PropertyChanged;

        [JsonInclude]
        [JsonPropertyName("lyrics")]
        // should be private, except JSON requires public
        public List<LyricsEntry> Entries { get; private set; } = new();

        [JsonIgnore]
        public ReadOnlyCollection<LyricsEntry> Lyrics => new(Entries.OrderBy(x => x, LyricsComparer.Instance).ToList());

        [JsonConstructor]
        public LyricsChannel(string? name = null)
        {
            this.name = name;
        }

        public void Add(LyricsEntry entry)
        {
            Entries.Add(entry);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Lyrics)));
        }

        public bool Remove(LyricsEntry entry)
        {
            if (Entries.Remove(entry))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Lyrics)));
                return true;
            }

            return false;
        }

        public void Clear()
        {
            if (Entries.Count > 0)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Lyrics)));
            Entries.Clear();
        }
    }

    public class LyricsEntry : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private string text;

        [JsonPropertyName("text")]
        public string Text
        {
            get => text;
            set
            {
                text = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
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

        [JsonConstructor]
        public LyricsEntry(string text, TimeSpan start, TimeSpan end)
        {
            this.text = text;
            this.start = start;
            this.end = end;
        }

        public string ToLrcEntry()
        {
            string time_str = StringUtils.TimeSpan(Start);
            return $"[{time_str}]{Text}";
        }

        public override string ToString()
        {
            return $"[{Start}]-[{End}]: {Text}";
        }
    }

    public class ChannelComparer : IComparer<LyricsChannel>
    {
        public static readonly ChannelComparer Instance = new();

        public int Compare(LyricsChannel? x, LyricsChannel? y)
        {
            if (x == null && y == null)
                return 0;
            else if (x == null)
                return -1;
            else if (y == null)
                return 1;
            int start = (x.Start ?? TimeSpan.MaxValue).CompareTo(y.Start ?? TimeSpan.MaxValue);
            if (start != 0)
                return start;
            int end = (x.End ?? TimeSpan.MaxValue).CompareTo(y.End ?? TimeSpan.MaxValue);
            if (end != 0)
                return end;
            return String.CompareOrdinal(x.Name, y.Name);
        }
    }

    public class LyricsComparer : IComparer<LyricsEntry>
    {
        public static readonly LyricsComparer Instance = new();

        public int Compare(LyricsEntry? x, LyricsEntry? y)
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
            return String.CompareOrdinal(x.Text, y.Text);
        }
    }
}