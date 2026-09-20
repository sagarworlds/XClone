using XCloneAPI.DTOs;

namespace XCloneAPI.Services
{
    public interface IUserService
    {
        Task<UserResponse> GetUserByIdAsync(int userId, int currentUserId);
        Task<UserResponse> GetUserByUsernameAsync(string username, int currentUserId);
        Task<List<UserResponse>> GetSuggestionsAsync(int currentUserId, int take);
        Task<List<UserResponse>> SearchUsersAsync(string query, int currentUserId, int take);
        Task<UserResponse> UpdateProfileAsync(int userId, UpdateProfileRequest request);
        Task<PagedResponse<UserResponse>> GetFollowersAsync(int userId, int currentUserId, int? beforeId, int take);
        Task<PagedResponse<UserResponse>> GetFollowingAsync(int userId, int currentUserId, int? beforeId, int take);
    }
}
