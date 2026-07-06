using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using XCloneAPI.Services;

namespace XCloneAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FollowsController : ControllerBase
    {
        private readonly IFollowService _followService;
        private readonly ILogger<FollowsController> _logger;

        public FollowsController(IFollowService followService, ILogger<FollowsController> logger)
        {
            _followService = followService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }

        [HttpPost("toggle/{userId}")]
        public async Task<IActionResult> ToggleFollow(int userId)
        {
            try
            {
                var followerId = GetCurrentUserId();
                if (followerId == userId)
                    return BadRequest(new { message = "Cannot follow yourself" });

                var result = await _followService.ToggleFollowAsync(followerId, userId);
                return Ok(new { followed = result });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error toggling follow: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("is-following/{userId}")]
        public async Task<IActionResult> IsFollowing(int userId)
        {
            try
            {
                var followerId = GetCurrentUserId();
                var isFollowing = await _followService.IsFollowingAsync(followerId, userId);
                return Ok(new { isFollowing });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking follow status: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }
}