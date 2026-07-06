namespace XCloneAPI.DTOs
{
    public class UserResponse
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string DisplayName { get; set; }
        public string Bio { get; set; }
        public string AvatarUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public int FollowersCount { get; set; }
        public int FollowingCount { get; set; }
        public bool IsFollowed { get; set; }
    }
}
