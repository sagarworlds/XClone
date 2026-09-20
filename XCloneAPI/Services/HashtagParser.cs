using System.Text.RegularExpressions;

namespace XCloneAPI.Services
{
    // Which words in a post are hashtags. The Angular app applies the same rule to link them (utils/text-entities.ts);
    // both are tested against the same list of examples (x-clone-frontend/src/testing/text-entities.json), so they
    // cannot drift apart.
    //
    // A hashtag is a # followed by 1-50 letters, digits or underscores, with at least one letter (so "#2026" is not
    // one). It must not be glued to a word or a symbol in front (so "abc#def", "##x" and "&#39;" are not hashtags),
    // and it ends where the letters end ("#tag," is "tag"). Tags are compared in lower case, each counts once per
    // post, and only the first 10 of a post count.
    public static partial class HashtagParser
    {
        public const int MaxTags = 10;
        public const int MaxLength = 50;

        // Letters, marks (vowel signs of many scripts are marks), digits and underscore
        [GeneratedRegex(@"(?<![\p{L}\p{M}\p{N}_#&])#([\p{L}\p{M}\p{N}_]{1,50})(?![\p{L}\p{M}\p{N}_])")]
        private static partial Regex InText();

        [GeneratedRegex(@"^[\p{L}\p{M}\p{N}_]{1,50}\z")]
        private static partial Regex WholeTag();

        // The hashtags of a text, lower case, without the #, in the order they first appear
        public static List<string> Parse(string? text)
        {
            var tags = new List<string>();
            if (string.IsNullOrEmpty(text))
                return tags;

            foreach (Match match in InText().Matches(text))
            {
                var tag = match.Groups[1].Value;
                if (!HasLetter(tag))
                    continue;

                tag = tag.ToLowerInvariant();
                if (tags.Contains(tag))
                    continue;

                tags.Add(tag);
                if (tags.Count == MaxTags)
                    break;
            }

            return tags;
        }

        // A tag as a client writes it in an address ("sunset", "#Sunset") -> the stored form; false when it cannot be a tag
        public static bool TryNormalize(string? input, out string tag)
        {
            tag = "";
            if (input == null)
                return false;

            var candidate = input.Trim();
            if (candidate.StartsWith('#'))
                candidate = candidate[1..];

            if (!WholeTag().IsMatch(candidate) || !HasLetter(candidate))
                return false;

            tag = candidate.ToLowerInvariant();
            return true;
        }

        private static bool HasLetter(string tag) => tag.Any(char.IsLetter);
    }
}
