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

        // Newest first. Ids only ever grow, so id order is time order, and a page starts below the id the cursor names.
        public async Task<PagedResponse<NotificationResponse>> GetNotificationsAsync(int userId, int? beforeId, int take)
        {
            try
            {
                var query = _context.Notifications.Where(n => n.RecipientId == userId);
                if (beforeId != null)
                    query = query.Where(n => n.Id < beforeId);

                // One more than asked for tells whether there is a next page
                var rows = await query
                    .OrderByDescending(n => n.Id)
                    .Take(take + 1)
                    .Include(n => n.Actor)
                    .Include(n => n.Post)
                    .AsNoTracking()
                    .ToListAsync();

                var hasMore = rows.Count > take;
                var notifications = hasMore ? rows.Take(take).ToList() : rows;

                var items = notifications.Select(n => new NotificationResponse
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

                return new PagedResponse<NotificationResponse>
                {
                    Items = items,
                    NextCursor = hasMore ? IdCursor.Encode(notifications[^1].Id) : null
                };
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
