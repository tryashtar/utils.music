using System.Linq;
using TagLib;
using TagLib.Id3v2;

namespace TryashtarUtils.Music
{
    public static class LanguageExtensions
    {
        public const string ID3_LANGUAGE_TAG = "TLAN";
        public const string XIPH_LANGUAGE_TAG = "LANGUAGE";

        public static string? Get(TagLib.Id3v2.Tag tag)
        {
            foreach (var frame in tag.GetFrames<TextInformationFrame>().ToList())
            {
                if (frame.FrameId.ToString() == ID3_LANGUAGE_TAG)
                {
                    if (frame.Text.Length > 0)
                        return frame.Text[0];
                }
            }
            return null;
        }

        public static string? Get(TagLib.Ogg.XiphComment tag)
        {
            return tag.GetFirstField(XIPH_LANGUAGE_TAG);
        }

        public static void Set(TagLib.Id3v2.Tag tag, string? value)
        {
            foreach (var frame in tag.GetFrames<TextInformationFrame>().ToList())
            {
                if (frame.FrameId.ToString() == ID3_LANGUAGE_TAG)
                    tag.RemoveFrame(frame);
            }
            if (value != null)
            {
                var lang = new TextInformationFrame(ByteVector.FromString(ID3_LANGUAGE_TAG, StringType.UTF8))
                {
                    Text = new[] { value }
                };
                tag.AddFrame(lang);
            }
        }

        public static void Set(TagLib.Ogg.XiphComment tag, string? value)
        {
            tag.SetField(XIPH_LANGUAGE_TAG, value);
        }
    }
}
