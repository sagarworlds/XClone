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

        public async Task<UserResponse> GetUserByUsernameAsync(string username, int currentUserId)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
                if (user == null)
                    return null;

                return await MapToUserResponseAsync(user, currentUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching user by username: {ex.Message}");
                throw;
            }
        }

        public async Task<List<UserResponse>> GetSuggestionsAsync(int currentUserId, int take)
        {
            try
            {
                var users = await _context.Users
                    .Where(u => u.Id != currentUserId
                        && !_context.Follows.Any(f => f.FollowerId == currentUserId && f.FollowingId == u.Id))
                    .OrderByDescending(u => u.CreatedAt)
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
                _logger.LogError($"Error fetching suggestions: {ex.Message}");
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

                if (request.Bio != null)
                    user.Bio = request.Bio;

                if (request.AvatarUrl != null)
                    user.AvatarUrl = request.AvatarUrl;

                user.UpdatedAt = DateTime.UtcNow;

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

        // Who follows the user, newest follow first. Follow ids only ever grow, so id order is time order, and a page
        // starts below the id the cursor names.
        public async Task<PagedResponse<UserResponse>> GetFollowersAsync(int userId, int currentUserId, int? beforeId, int take)
        {
            try
            {
                var follows = _context.Follows.Where(f => f.FollowingId == userId);
                if (beforeId != null)
                    follows = follows.Where(f => f.Id < beforeId);

                // One more than asked for tells whether there is a next page
                var rows = await follows
                    .OrderByDescending(f => f.Id)
                    .Take(take + 1)
                    .Select(f => new FollowRow { FollowId = f.Id, User = f.Follower })
                    .ToListAsync();

                return await BuildUserPageAsync(rows, take, currentUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching followers: {ex.Message}");
                throw;
            }
        }

        // Who the user follows, newest follow first.
        public async Task<PagedResponse<UserResponse>> GetFollowingAsync(int userId, int currentUserId, int? beforeId, int take)
        {
            try
            {
                var follows = _context.Follows.Where(f => f.FollowerId == userId);
                if (beforeId != null)
                    follows = follows.Where(f => f.Id < beforeId);

                var rows = await follows
                    .OrderByDescending(f => f.Id)
                    .Take(take + 1)
                    .Select(f => new FollowRow { FollowId = f.Id, User = f.Following })
                    .ToListAsync();

                return await BuildUserPageAsync(rows, take, currentUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching following: {ex.Message}");
                throw;
            }
        }

        // One row of a followers/following list: the follow (which is what the cursor points at) and the user on the
        // other end of it.
        private sealed class FollowRow
        {
            public int FollowId { get; set; }
            public Models.User User { get; set; } = null!;
        }

        // The extra row of a query that asked for one more than the page size is not shown, it only proves there is
        // more; the cursor points at the last row that is shown.
        private async Task<PagedResponse<UserResponse>> BuildUserPageAsync(List<FollowRow> rows, int take, int currentUserId)
        {
            var hasMore = rows.Count > take;
            var page = hasMore ? rows.Take(take).ToList() : rows;

            var users = new List<UserResponse>();
            foreach (var row in page)
            {
                users.Add(await MapToUserResponseAsync(row.User, currentUserId));
            }

            return new PagedResponse<UserResponse>
            {
                Items = users,
                NextCursor = hasMore ? IdCursor.Encode(page[^1].FollowId) : null
            };
        }

        private async Task<UserResponse> MapToUserResponseAsync(Models.User user, int currentUserId)
        {
            var followersCount = await _context.Follows.CountAsync(f => f.FollowingId == user.Id);
            var followingCount = await _context.Follows.CountAsync(f => f.FollowerId == user.Id);
            var isFollowed = await _context.Follows.AnyAsync(f => f.FollowerId == currentUserId && f.FollowingId == user.Id);
            var postsCount = await _context.Posts.CountAsync(p => p.UserId == user.Id && p.ParentPostId == null)
                + await _context.Retweets.CountAsync(r => r.UserId == user.Id);

            return new UserResponse
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Id == currentUserId ? user.Email : string.Empty,
                DisplayName = user.DisplayName,
                Bio = user.Bio,
                AvatarUrl = user.AvatarUrl,
                CreatedAt = user.CreatedAt,
                FollowersCount = followersCount,
                FollowingCount = followingCount,
                PostsCount = postsCount,
                IsFollowed = isFollowed
            };
        }
    }
}