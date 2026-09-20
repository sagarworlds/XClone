using System.Text.RegularExpressions;

namespace XCloneAPI.Services
{
    public enum ImageKind
    {
        Png,
        Jpeg,
        Gif,
        Webp
    }

    // What an uploaded image is called and how it is told apart from other files. The name is made by the server
    // (never taken from the client), and the kind comes from the file's own first bytes (never from the client's
    // file name or content type), so an upload cannot be something other than an image or reach another folder.
    public static partial class MediaNames
    {
        public const string UrlPrefix = "/uploads/";

        // \z, not $: $ would also accept a name followed by a line break
        [GeneratedRegex(@"^[0-9a-f]{32}\.(png|jpg|gif|webp)\z", RegexOptions.CultureInvariant)]
        private static partial Regex NamePattern();

        // A new random name for an image of this kind
        public static string NewName(ImageKind kind) => $"{Guid.NewGuid():N}.{Extension(kind)}";

        public static bool IsValidName(string? name) => name != null && NamePattern().IsMatch(name);

        public static string ToUrl(string name) => UrlPrefix + name;

        // "/uploads/<name>" -> "<name>", and only exactly that: other hosts, paths, cases and extensions are refused
        public static bool TryGetName(string? url, out string name)
        {
            name = "";
            if (url == null || !url.StartsWith(UrlPrefix, StringComparison.Ordinal))
                return false;

            var candidate = url[UrlPrefix.Length..];
            if (!IsValidName(candidate))
                return false;

            name = candidate;
            return true;
        }

        public static string Extension(ImageKind kind) => kind switch
        {
            ImageKind.Png => "png",
            ImageKind.Jpeg => "jpg",
            ImageKind.Gif => "gif",
            ImageKind.Webp => "webp",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        public static string ContentType(ImageKind kind) => kind switch
        {
            ImageKind.Png => "image/png",
            ImageKind.Jpeg => "image/jpeg",
            ImageKind.Gif => "image/gif",
            ImageKind.Webp => "image/webp",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        // The number of bytes Detect needs to see
        public const int HeaderLength = 12;

        // PNG: 89 'PNG' CR LF 1A LF.  JPEG: FF D8 FF.  GIF: "GIF87a" or "GIF89a".  WebP: "RIFF" size "WEBP".
        // Anything else (SVG, HTML, text, an empty file...) is not accepted.
        public static ImageKind? Detect(ReadOnlySpan<byte> header)
        {
            if (header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
                return ImageKind.Png;

            if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                return ImageKind.Jpeg;

            if (header.Length >= 6
                && header[..4].SequenceEqual("GIF8"u8)
                && (header[4] == (byte)'7' || header[4] == (byte)'9')
                && header[5] == (byte)'a')
                return ImageKind.Gif;

            if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
                return ImageKind.Webp;

            return null;
        }
    }
}
