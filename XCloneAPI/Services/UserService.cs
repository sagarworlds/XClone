using Microsoft.EntityFrameworkCore;
using XCloneAPI.Data;
using XCloneAPI.DTOs;

namespace XCloneAPI.Services
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UserService> _logger;

        public UserService(AppDbContext context, ILogger<UserService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<UserResponse> GetUserByIdAsync(int userId, int currentUserId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return null;

                return await MapToUserResponseAsync(user, currentUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching user: {ex.Message}");
                throw;
            }
        }

        public async Task<List<UserResponse>> SearchUsersAsync(string query, int currentUserId, int take)
        {
            try
            {
                var users = await _context.Users
                    .Where(u => u.Username.Contains(query) || u.DisplayName.Contains(query))
                    .Take(take)
                    .ToListAsync();

                var responses = new List<UserResponse>();
                foreach (var user in users)
                {
                    responses.Add(await MapToUserResponseAsync(user, currentUserId));
                }

                return responses;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error searching users: {ex.Message}");
                throw;
            }
        }

        public async Task<UserResponse> UpdateProfileAsync(int userId, UpdateProfileRequest request)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return null;

                if (!string.IsNullOrWhiteSpace(request.DisplayName))
                    user.DisplayName = request.DisplayName;

                if (!string.IsNullOrWhiteSpace(request.Bio))
                    user.Bio = request.Bio;

                if (!string.IsNullOrWhiteSpace(request.AvatarUrl))
                    user.AvatarUrl = request.AvatarUrl;

                user.UpdatedAt = DateTime.UtcNow;

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"User {userId} profile updated");

                return await MapToUserResponseAsync(user, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating profile: {ex.Message}");
                throw;
            }
        }

        public async Task<List<UserResponse>> GetFollowersAsync(int userId, int currentUserId, int skip, int take)
        {
            try
            {
                var followers = await _context.Follows
                    .Where(f => f.FollowingId == userId)
                    .Include(f => f.Follower)
                    .Skip(skip)
                    .Take(take)
                    .Select(f => f.Follower)
                    .ToListAsync();

                var responses = new List<UserResponse>();
                foreach (var follower in followers)
                {
                    responses.Add(await MapToUserResponseAsync(follower, currentUserId));
                }

                return responses;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching followers: {ex.Message}");
                throw;
            }
        }

        public async Task<List<UserResponse>> GetFollowingAsync(int userId, int currentUserId, int skip, int take)
        {
            try
            {
                var following = await _context.Follows
                    .Where(f => f.FollowerId == userId)
                    .Include(f => f.Following)
                    .Skip(skip)
                    .Take(take)
                    .Select(f => f.Following)
                    .ToListAsync();

                var responses = new List<UserResponse>();
                foreach (var user in following)
                {
                    responses.Add(await MapToUserResponseAsync(user, currentUserId));
                }

                return responses;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching following: {ex.Message}");
                throw;
            }
        }

        private async Task<UserResponse> MapToUserResponseAsync(Models.User user, int currentUserId)
        {
            var followersCount = await _context.Follows.CountAsync(f => f.FollowingId == user.Id);
            var followingCount = await _context.Follows.CountAsync(f => f.FollowerId == user.Id);
            var isFollowed = await _context.Follows.AnyAsync(f => f.FollowerId == currentUserId && f.FollowingId == user.Id);

            return new UserResponse
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                DisplayName = user.DisplayName,
                Bio = user.Bio,
                AvatarUrl = user.AvatarUrl,
                CreatedAt = user.CreatedAt,
                FollowersCount = followersCount,
                FollowingCount = followingCount,
                IsFollowed = isFollowed
            };
        }
    }
}