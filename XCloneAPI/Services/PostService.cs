using Microsoft.EntityFrameworkCore;
using XCloneAPI.Data;
using XCloneAPI.DTOs;
using XCloneAPI.Models;

namespace XCloneAPI.Services
{
    public class PostService : IPostService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PostService> _logger;

        public PostService(AppDbContext context, ILogger<PostService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<PostResponse> CreatePostAsync(int userId, PostRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Content))
                    throw new ArgumentException("Post content cannot be empty");

                var post = new Post
                {
                    UserId = userId,
                    Content = request.Content,
                    MediaUrls = request.MediaUrls ?? Array.Empty<string>()
                };

                _context.Posts.Add(post);
                await _context.SaveChangesAsync();

                var user = await _context.Users.FindAsync(userId);
                _logger.LogInformation($"Post created by user {userId}");

                return MapToPostResponse(post, user, false);
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating post: {ex.Message}");
                throw;
            }
        }

        public async Task<PostResponse> GetPostByIdAsync(int postId, int currentUserId)
        {
            try
            {
                var post = await _context.Posts
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.Id == postId);

                if (post == null)
                    return null;

                var isLiked = await _context.Likes
                    .AnyAsync(l => l.PostId == postId && l.UserId == currentUserId);

                return MapToPostResponse(post, post.User, isLiked);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching post: {ex.Message}");
                throw;
            }
        }

        public async Task<List<PostResponse>> GetFeedAsync(int userId, int skip, int take)
        {
            try
            {
                var posts = await _context.Posts
                    .Include(p => p.User)
                    .Where(p => _context.Follows
                        .Any(f => f.FollowerId == userId && f.FollowingId == p.UserId) || p.UserId == userId)
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();

                var responses = new List<PostResponse>();
                foreach (var post in posts)
                {
                    var isLiked = await _context.Likes
                        .AnyAsync(l => l.PostId == post.Id && l.UserId == userId);
                    responses.Add(MapToPostResponse(post, post.User, isLiked));
                }

                return responses;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching feed: {ex.Message}");
                throw;
            }
        }

        public async Task<List<PostResponse>> GetUserPostsAsync(int userId, int currentUserId, int skip, int take)
        {
            try
            {
                var posts = await _context.Posts
                    .Include(p => p.User)
                    .Where(p => p.UserId == userId)
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();

                var responses = new List<PostResponse>();
                foreach (var post in posts)
                {
                    var isLiked = await _context.Likes
                        .AnyAsync(l => l.PostId == post.Id && l.UserId == currentUserId);
                    responses.Add(MapToPostResponse(post, post.User, isLiked));
                }

                return responses;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching user posts: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> DeletePostAsync(int postId, int userId)
        {
            try
            {
                var post = await _context.Posts.FindAsync(postId);
                if (post == null || post.UserId != userId)
                    return false;

                _context.Posts.Remove(post);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Post {postId} deleted by user {userId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting post: {ex.Message}");
                throw;
            }
        }

        private PostResponse MapToPostResponse(Post post, User user, bool isLiked)
        {
            return new PostResponse
            {
                Id = post.Id,
                UserId = post.UserId,
                Content = post.Content,
                MediaUrls = post.MediaUrls,
                LikesCount = post.LikesCount,
                RetweetsCount = post.RetweetsCount,
                RepliesCount = post.RepliesCount,
                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt,
                IsLiked = isLiked,
                User = new UserResponse
                {
                    Id = user.Id,
                    Username = user.Username,
                    DisplayName = user.DisplayName,
                    AvatarUrl = user.AvatarUrl
                }
            };
        }
    }
}
