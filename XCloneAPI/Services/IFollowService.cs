namespace XCloneAPI.Services
{
    public interface IFollowService
    {
        Task<bool> ToggleFollowAsync(int followerId, int followingId);
        Task<bool> IsFollowingAsync(int followerId, int followingId);
    }
}