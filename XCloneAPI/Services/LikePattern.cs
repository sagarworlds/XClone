namespace XCloneAPI.Services
{
    // Turns text someone typed into a literal ILIKE pattern. ILIKE treats % and _ as wildcards and \ as its escape
    // character, so without this a search for "50% off" or "a_b" would match far more than that exact text.
    public static class LikePattern
    {
        public const string EscapeCharacter = "\\";

        // "%<the text, wildcards taken literally>%", for EF.Functions.ILike(column, Contains(text), EscapeCharacter)
        public static string Contains(string text) =>
            $"%{text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
    }
}
