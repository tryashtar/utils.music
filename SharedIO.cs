using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TagLib;
using TagLib.Id3v2;
using File = System.IO.File;
using Tag = TagLib.Tag;

namespace TryashtarUtils.Music
{
    internal static class SharedIO
    {
        public static readonly Regex LrcRegex = new(@"\[(?<time>.+)\](?<line>.+)");
        public static readonly string[] TimespanFormats = new string[] { @"h\:mm\:ss\.FFF", @"mm\:ss\.FFF", @"m\:ss\.FFF", @"h\:mm\:ss", @"mm\:ss", @"m\:ss" };

        public static T? FromMany<T>(IEnumerable<Func<T?>> methods) where T : class
        {
            foreach (var method in methods)
            {
                var item = method();
                if (item != null)
                    return item;
            }
            return null;
        }

        public static Func<U?> MethodAttempt<T, U>(Func<T?> setup, Func<T, U?> getter) where U : class
        {
            return () =>
            {
                var item = setup();
                if (item == null)
                    return null;
                return getter(item);
            };
        }
    }
}
