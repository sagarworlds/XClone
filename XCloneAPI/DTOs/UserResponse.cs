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

        // What the profile's Posts tab lists: the user's top-level posts plus their reposts (replies are not counted).
        // Filled in wherever profiles are returned; 0 in the short author summaries nested inside posts.
        public int PostsCount { get; set; }
        public bool IsFollowed { get; set; }
    }
}
