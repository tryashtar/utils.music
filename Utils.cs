using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TagLib;
using TagLib.Id3v2;

namespace TryashtarUtils.Music
{
    public static class Utils
    {
        private const string OGG_LYRICS = "SYNCED LYRICS";
        public static void WriteLyrics(TagLib.File file, Lyrics lyrics)
        {
            var tag = file.Tag;
            var id3v2 = (TagLib.Id3v2.Tag)file.GetTag(TagTypes.Id3v2);
            var ogg = (TagLib.Ogg.XiphComment)file.GetTag(TagTypes.Xiph);

            tag.Lyrics = lyrics.ToSimple();
            if (id3v2 != null)
            {
                foreach (var frame in id3v2.GetFrames<SynchronisedLyricsFrame>().ToList())
                {
                    id3v2.RemoveFrame(frame);
                }
                var new_frame = new SynchronisedLyricsFrame("lyrics", null, SynchedTextType.Lyrics, StringType.Latin1);
                new_frame.Text = lyrics.ToSynchedText();
                id3v2.AddFrame(new_frame);
            }
            if (ogg != null)
            {
                ogg.SetField(OGG_LYRICS, lyrics.ToLrc());
            }
        }

        public static Lyrics ReadLyrics(TagLib.File file)
        {
            var id3v2 = (TagLib.Id3v2.Tag)file.GetTag(TagTypes.Id3v2);
            if (id3v2 != null)
            {
                foreach (var frame in id3v2.GetFrames<SynchronisedLyricsFrame>())
                {
                    return new Lyrics(frame.Text);
                }
            }
            var ogg = (TagLib.Ogg.XiphComment)file.GetTag(TagTypes.Xiph);
            if (ogg != null)
            {
                var lyrics = ogg.GetField(OGG_LYRICS);
                if (lyrics != null && lyrics.Length > 0)
                    return Lyrics.FromLrc(lyrics);
            }
            var tag = file.Tag;
            if (!String.IsNullOrEmpty(tag.Lyrics))
                return new Lyrics(tag.Lyrics);
            return null;
        }
    }
}
