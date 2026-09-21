using System.Text.RegularExpressions;

namespace XCloneAPI.Services
{
    // Which words in a post are mentions ("@username"). The Angular app applies the same rule to find them
    // (utils/text-entities.ts); both are tested against the same list of examples
    // (x-clone-frontend/src/testing/text-entities.json).
    //
    // A mention is an @ followed by 3-50 letters (a-z), digits or underscores, the characters a username is made of.
    // It must not be glued to a word in front (so an email address like "me@example.com" and "a@@b" are not mentions)
    // and it ends where the word ends (so "@bob," is "bob", but "@bobé" is no mention). Names are compared without
    // regard to case, each counts once per post (as it was first written), and only the first 10 of a post count.
    public static partial class MentionParser
    {
        public const int MaxMentions = 10;

        [GeneratedRegex(@"(?<![\p{L}\p{M}\p{N}_@])@([A-Za-z0-9_]{3,50})(?![\p{L}\p{M}\p{N}_])")]
        private static partial Regex InText();

        // The names mentioned in a text, as first written (without the @), at most 10
        public static List<string> Parse(string? text)
        {
            var names = new List<string>();
            if (string.IsNullOrEmpty(text))
                return names;

            foreach (Match match in InText().Matches(text))
            {
                var name = match.Groups[1].Value;
                if (names.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                names.Add(name);
                if (names.Count == MaxMentions)
                    break;
            }

            return names;
        }
    }
}
