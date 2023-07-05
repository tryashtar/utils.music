using System;
using System.Linq;
using System.Text;
using TagLib;
using TagLib.Id3v2;

namespace TryashtarUtils.Music
{
    public static class LanguageExtensions
    {
        public const string ID3_LANGUAGE_TAG = "TLAN";
        public const string XIPH_LANGUAGE_TAG = "LANGUAGE";

        public static string? GetId3v2(TagLib.Id3v2.Tag tag)
        {
            return tag.GetFrames<TextInformationFrame>()
                .FirstOrDefault(x => x.FrameId.ToString() == ID3_LANGUAGE_TAG && x.Text.Length > 0)?.Text[0];
        }

        public static string? GetXiph(TagLib.Ogg.XiphComment tag)
        {
            return tag.GetFirstField(XIPH_LANGUAGE_TAG);
        }

        public static bool SetId3v2(TagLib.Id3v2.Tag tag, string? value)
        {
            var existing = tag.GetFrames<TextInformationFrame>().Where(x => x.FrameId.ToString() == ID3_LANGUAGE_TAG)
                .ToList();
            if (value != null)
            {
                var lang = new TextInformationFrame(ByteVector.FromString(ID3_LANGUAGE_TAG, StringType.UTF8))
                {
                    Text = new[] { value },
                    TextEncoding = StringType.UTF16
                };
                if (existing.Count == 1 && existing[0].Render(4) == lang.Render(4))
                    return false;
                foreach (var frame in existing)
                {
                    tag.RemoveFrame(frame);
                }

                tag.AddFrame(lang);
                return true;
            }
            else
            {
                foreach (var frame in existing)
                {
                    tag.RemoveFrame(frame);
                }

                return existing.Count > 0;
            }
        }

        public static bool SetXiph(TagLib.Ogg.XiphComment tag, string? value)
        {
            var existing = tag.GetField(XIPH_LANGUAGE_TAG);
            tag.SetField(XIPH_LANGUAGE_TAG, value);
            if (value == null)
                return existing.Length != 0;
            else
                return existing.Length != 1 || existing[0] != value;
        }
    }
}