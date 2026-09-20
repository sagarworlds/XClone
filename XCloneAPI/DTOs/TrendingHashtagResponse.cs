namespace XCloneAPI.DTOs
{
    public class TrendingHashtagResponse
    {
        // Lower case, without the #
        public string Tag { get; set; } = "";

        // How many posts used it in the period
        public int PostsCount { get; set; }
    }
}
