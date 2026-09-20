using Microsoft.EntityFrameworkCore;
using XCloneAPI.Data;
using XCloneAPI.Models;

namespace XCloneAPI.Services
{
    public class RetweetService : IRetweetService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<RetweetService> _logger;

        public RetweetService(AppDbContext context, ILogger<RetweetService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> ToggleRetweetAsync(int userId, int postId)
        {
            try
            {
                var post = await _context.Posts.FindAsync(postId);
                if (post == null)
                    throw new ArgumentException("Post not found");

                var existing = await _context.Retweets
                    .FirstOrDefaultAsync(r => r.UserId == userId && r.PostId == postId);

                if (existing != null)
                {
                    _context.Retweets.Remove(existing);
                    if (post.RetweetsCount > 0)
                        post.RetweetsCount--;
                }
                else
                {
                    _context.Retweets.Add(new Retweet { UserId = userId, PostId = postId });
                    post.RetweetsCount++;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation($"User {userId} toggled retweet on post {postId}");
                return existing == null;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error toggling retweet: {ex.Message}");
                throw;
            }
        }
    }
}
