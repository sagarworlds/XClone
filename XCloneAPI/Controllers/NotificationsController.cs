using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using XCloneAPI.Services;

namespace XCloneAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<NotificationsController> _logger;
        private const int MaxPageSize = 50;

        public NotificationsController(INotificationService notificationService, ILogger<NotificationsController> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications([FromQuery] string? cursor = null, [FromQuery] int take = 20)
        {
            try
            {
                if (!IdCursor.TryParse(cursor, out var beforeId))
                    return BadRequest(new { message = "Invalid cursor" });

                take = Math.Clamp(take, 1, MaxPageSize);
                var notifications = await _notificationService.GetNotificationsAsync(GetCurrentUserId(), beforeId, take);
                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting notifications: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            try
            {
                var count = await _notificationService.GetUnreadCountAsync(GetCurrentUserId());
                return Ok(new { count });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error counting unread notifications: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            try
            {
                await _notificationService.MarkAllAsReadAsync(GetCurrentUserId());
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error marking notifications as read: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }
}
