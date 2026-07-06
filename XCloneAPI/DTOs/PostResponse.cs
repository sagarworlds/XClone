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
    }
}
