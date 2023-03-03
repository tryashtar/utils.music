using System;
using System.Linq;
using TagLib;
using TagLib.Id3v2;

namespace TryashtarUtils.Music
{
    public static class PictureExtensions
    {
        public static IPicture? Get(TagLib.Id3v2.Tag tag)
        {
            return tag.GetFrames<AttachmentFrame>().FirstOrDefault();
        }

        public static IPicture? Get(TagLib.Flac.Metadata tag)
        {
            return tag.Pictures.FirstOrDefault();
        }

        public static bool Set(TagLib.Id3v2.Tag tag, IPicture? picture)
        {
            var existing = tag.GetFrames<AttachmentFrame>().ToList();
            foreach (var frame in existing)
            {
                tag.RemoveFrame(frame);
            }

            if (picture == null)
                return existing.Count > 0;
            var attachment = new AttachmentFrame(picture);
            tag.AddFrame(attachment);
            return existing.Count != 1 || existing[0].Data == picture.Data;
        }

        public static bool Set(TagLib.Flac.Metadata tag, IPicture? picture)
        {
            var existing = tag.Pictures;

            if (picture == null)
            {
                tag.Pictures = Array.Empty<IPicture>();
                return existing.Length > 0;
            }

            tag.Pictures = new[] { picture };
            return existing.Length != 1 || existing[0].Data == picture.Data;
        }
    }
}