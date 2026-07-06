using Microsoft.EntityFrameworkCore;
using XCloneAPI.Data;
using XCloneAPI.Models;

namespace XCloneAPI.Services
{
    public class FollowService : IFollowService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<FollowService> _logger;

        public FollowService(AppDbContext context, ILogger<FollowService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> ToggleFollowAsync(int followerId, int followingId)
        {
            try
            {
                var userExists = await _context.Users.AnyAsync(u => u.Id == followingId);
                if (!userExists)
                    throw new ArgumentException("User not found");

                var existingFollow = await _context.Follows
                    .FirstOrDefaultAsync(f => f.FollowerId == followerId && f.FollowingId == followingId);

                if (existingFollow != null)
                {
                    _context.Follows.Remove(existingFollow);
                }
                else
                {
                    var follow = new Follow
                    {
                        FollowerId = followerId,
                        FollowingId = followingId
                    };
                    _context.Follows.Add(follow);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation($"User {followerId} toggled follow on user {followingId}");
                return existingFollow == null;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error toggling follow: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> IsFollowingAsync(int followerId, int followingId)
        {
            try
            {
                return await _context.Follows
                    .AnyAsync(f => f.FollowerId == followerId && f.FollowingId == followingId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking follow status: {ex.Message}");
                throw;
            }
        }
    }
}