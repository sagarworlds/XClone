using System.Globalization;
using System.Text;

namespace XCloneAPI.Services
{
    // Cursors are how clients page through a list: the sort key of the last entry they have seen, packed into an
    // opaque string. The next page starts strictly after that key, so entries that appear or disappear elsewhere in
    // the list cannot shift a page (skip/take repeats or skips entries when that happens).
    public static class PageCursor
    {
        private const int MaxLength = 100;

        public static string Encode(params long[] parts)
        {
            var text = string.Join(':', parts);
            return Convert.ToBase64String(Encoding.ASCII.GetBytes(text))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        // No cursor at all (null or empty) is valid and means "the first page". Anything else has to decode to
        // exactly `expectedParts` non-negative whole numbers.
        public static bool TryDecode(string? cursor, int expectedParts, out long[]? parts)
        {
            parts = null;
            if (string.IsNullOrEmpty(cursor))
                return true;
            if (cursor.Length > MaxLength)
                return false;

            try
            {
                var base64 = cursor.Replace('-', '+').Replace('_', '/');
                base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
                var pieces = Encoding.ASCII.GetString(Convert.FromBase64String(base64)).Split(':');
                if (pieces.Length != expectedParts)
                    return false;

                var values = new long[pieces.Length];
                for (var i = 0; i < pieces.Length; i++)
                {
                    if (!long.TryParse(pieces[i], NumberStyles.None, CultureInfo.InvariantCulture, out values[i]))
                        return false;
                }

                parts = values;
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }

    // Position in a timeline of original posts and reposts, newest first. Several entries can share a moment or a
    // post, so the key is the moment, the post and who reposted it (0 for the original), which is unique per entry.
    public sealed record TimelineCursor(DateTime At, int PostId, int RetweeterId)
    {
        public string Encode() => PageCursor.Encode(At.Ticks, PostId, RetweeterId);

        // True when the cursor is absent (result stays null) or valid.
        public static bool TryParse(string? cursor, out TimelineCursor? result)
        {
            result = null;
            if (!PageCursor.TryDecode(cursor, 3, out var parts))
                return false;
            if (parts == null)
                return true;
            if (parts[0] > DateTime.MaxValue.Ticks || parts[1] > int.MaxValue || parts[2] > int.MaxValue)
                return false;

            result = new TimelineCursor(new DateTime(parts[0], DateTimeKind.Utc), (int)parts[1], (int)parts[2]);
            return true;
        }
    }

    // Position in a list that is ordered by id (replies, notifications).
    public static class IdCursor
    {
        public static string Encode(int id) => PageCursor.Encode(id);

        // True when the cursor is absent (id stays null) or valid.
        public static bool TryParse(string? cursor, out int? id)
        {
            id = null;
            if (!PageCursor.TryDecode(cursor, 1, out var parts))
                return false;
            if (parts == null)
                return true;
            if (parts[0] > int.MaxValue)
                return false;

            id = (int)parts[0];
            return true;
        }
    }
}
