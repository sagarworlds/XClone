using XCloneAPI.DTOs;

namespace XCloneAPI.Services
{
    public interface INotificationService
    {
        Task<PagedResponse<NotificationResponse>> GetNotificationsAsync(int userId, int? beforeId, int take);
        Task<int> GetUnreadCountAsync(int userId);
        Task MarkAllAsReadAsync(int userId);
    }
}
