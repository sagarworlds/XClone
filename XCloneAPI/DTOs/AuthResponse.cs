namespace XCloneAPI.DTOs
{
    public class AuthResponse
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string DisplayName { get; set; }
        public string AvatarUrl { get; set; }
        public string Token { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}