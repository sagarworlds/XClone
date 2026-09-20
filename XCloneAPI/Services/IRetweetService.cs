namespace XCloneAPI.Services
{
    public interface IRetweetService
    {
        Task<bool> ToggleRetweetAsync(int userId, int postId);
    }
}
