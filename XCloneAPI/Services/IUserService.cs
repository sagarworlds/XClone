using XCloneAPI.DTOs;

namespace XCloneAPI.Services
{
    public interface IUserService
    {
        Task<UserResponse> GetUserByIdAsync(int userId, int currentUserId);
        Task<List<UserResponse>> SearchUsersAsync(string query, int currentUserId, int take);
        Task<UserResponse> UpdateProfileAsync(int userId, UpdateProfileRequest request);
        Task<List<UserResponse>> GetFollowersAsync(int userId, int currentUserId, int skip, int take);
        Task<List<UserResponse>> GetFollowingAsync(int userId, int currentUserId, int skip, int take);
    }
}
