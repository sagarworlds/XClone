using XCloneAPI.DTOs;

namespace XCloneAPI.Services
{
    public interface INotificationService
    {
        Task<List<NotificationResponse>> GetNotificationsAsync(int userId, int skip, int take);
        Task<int> GetUnreadCountAsync(int userId);
        Task MarkAllAsReadAsync(int userId);
    }
}
