using Microsoft.EntityFrameworkCore;
using XCloneAPI.Data;
using XCloneAPI.DTOs;
using XCloneAPI.Models;

namespace XCloneAPI.Services
{
    // Notifications are created by the services that cause them (PostService for replies, RetweetService for
    // reposts), in the same save as the action itself. This service only reads them and marks them read.
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(AppDbContext context, ILogger<NotificationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<NotificationResponse>> GetNotificationsAsync(int userId, int skip, int take)
        {
            try
            {
                var notifications = await _context.Notifications
                    .Where(n => n.RecipientId == userId)
                    .OrderByDescending(n => n.CreatedAt)
                    .ThenByDescending(n => n.Id)
                    .Skip(skip)
                    .Take(take)
                    .Include(n => n.Actor)
                    .Include(n => n.Post)
                    .AsNoTracking()
                    .ToListAsync();

                return notifications.Select(n => new NotificationResponse
                {
                    Id = n.Id,
                    Type = n.Type.ToString().ToLowerInvariant(),
                    Actor = new UserResponse
                    {
                        Id = n.Actor.Id,
                        Username = n.Actor.Username,
                        DisplayName = n.Actor.DisplayName,
                        AvatarUrl = n.Actor.AvatarUrl
                    },
                    PostId = n.PostId,
                    PostContent = n.Post.Content,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting notifications: {ex.Message}");
                throw;
            }
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            try
            {
                return await _context.Notifications.CountAsync(n => n.RecipientId == userId && !n.IsRead);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error counting unread notifications: {ex.Message}");
                throw;
            }
        }

        public async Task MarkAllAsReadAsync(int userId)
        {
            try
            {
                await _context.Notifications
                    .Where(n => n.RecipientId == userId && !n.IsRead)
                    .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error marking notifications as read: {ex.Message}");
                throw;
            }
        }
    }
}
