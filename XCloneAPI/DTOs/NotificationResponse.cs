namespace XCloneAPI.DTOs
{
    public class NotificationResponse
    {
        public int Id { get; set; }

        // "reply" or "repost"
        public string Type { get; set; }

        // Who replied / reposted
        public UserResponse Actor { get; set; }

        // The post to open: the reply itself, or the post that was reposted
        public int PostId { get; set; }
        public string PostContent { get; set; }

        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
