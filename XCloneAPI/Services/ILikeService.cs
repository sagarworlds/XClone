using XCloneAPI.DTOs;

namespace XCloneAPI.Services
{
    public interface ILikeService
    {
        Task<bool> ToggleLikeAsync(int userId, int postId);
        Task<List<UserResponse>> GetPostLikesAsync(int postId, int skip, int take);
    }
}