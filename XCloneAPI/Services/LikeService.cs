using Microsoft.EntityFrameworkCore;
using XCloneAPI.Data;
using XCloneAPI.DTOs;
using XCloneAPI.Models;

namespace XCloneAPI.Services
{
    public class LikeService : ILikeService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<LikeService> _logger;

        public LikeService(AppDbContext context, ILogger<LikeService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> ToggleLikeAsync(int userId, int postId)
        {
            try
            {
                var post = await _context.Posts.FindAsync(postId);
                if (post == null)
                    throw new ArgumentException("Post not found");

                var existingLike = await _context.Likes
                    .FirstOrDefaultAsync(l => l.UserId == userId && l.PostId == postId);

                if (existingLike != null)
                {
                    _context.Likes.Remove(existingLike);
                    post.LikesCount--;
                }
                else
                {
                    var like = new Like
                    {
                        UserId = userId,
                        PostId = postId
                    };
                    _context.Likes.Add(like);
                    post.LikesCount++;
                }

                post.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"User {userId} toggled like on post {postId}");
                return existingLike == null;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error toggling like: {ex.Message}");
                throw;
            }
        }

        public async Task<List<UserResponse>> GetPostLikesAsync(int postId, int skip, int take)
        {
            try
            {
                var post = await _context.Posts.FindAsync(postId);
                if (post == null)
                    throw new ArgumentException("Post not found");

                var likers = await _context.Likes
                    .Where(l => l.PostId == postId)
                    .Include(l => l.User)
                    .Skip(skip)
                    .Take(take)
                    .Select(l => l.User)
                    .ToListAsync();

                var responses = new List<UserResponse>();
                foreach (var user in likers)
                {
                    responses.Add(new UserResponse
                    {
                        Id = user.Id,
                        Username = user.Username,
                        Email = string.Empty,
                        DisplayName = user.DisplayName,
                        Bio = user.Bio,
                        AvatarUrl = user.AvatarUrl,
                        CreatedAt = user.CreatedAt
                    });
                }

                return responses;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching post likes: {ex.Message}");
                throw;
            }
        }
    }
}
