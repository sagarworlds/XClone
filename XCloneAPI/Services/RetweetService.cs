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

                    // Undoing a repost takes back the notification about it
                    var notifications = await _context.Notifications
                        .Where(n => n.Type == NotificationType.Repost && n.ActorId == userId && n.PostId == postId)
                        .ToListAsync();
                    _context.Notifications.RemoveRange(notifications);
                }
                else
                {
                    _context.Retweets.Add(new Retweet { UserId = userId, PostId = postId });
                    post.RetweetsCount++;

                    // Tell the author (not when reposting your own post)
                    if (post.UserId != userId)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            RecipientId = post.UserId,
                            ActorId = userId,
                            Type = NotificationType.Repost,
                            PostId = postId
                        });
                    }
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
