namespace XCloneAPI.DTOs
{
    public class PostResponse
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Content { get; set; }
        public string[] MediaUrls { get; set; }
        public int LikesCount { get; set; }
        public int RetweetsCount { get; set; }
        public int RepliesCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public UserResponse User { get; set; }
        public bool IsLiked { get; set; }

        // Reply info: set when this post is a reply
        public int? ParentPostId { get; set; }
        public string? ReplyToUsername { get; set; }

        // The usernames (as they are spelled on their profiles) that the text names with @ and that exist; the app
        // links only these
        public string[] Mentions { get; set; } = Array.Empty<string>();

        // Retweet info: whether the current user retweeted it, and (in timelines) who retweeted it into this entry
        public bool IsRetweeted { get; set; }
        public UserResponse? RetweetedBy { get; set; }
    }
}
