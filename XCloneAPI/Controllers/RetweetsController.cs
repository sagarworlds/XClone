using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using XCloneAPI.Services;

namespace XCloneAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RetweetsController : ControllerBase
    {
        private readonly IRetweetService _retweetService;
        private readonly ILogger<RetweetsController> _logger;

        public RetweetsController(IRetweetService retweetService, ILogger<RetweetsController> logger)
        {
            _retweetService = retweetService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }

        [HttpPost("toggle/{postId}")]
        public async Task<IActionResult> ToggleRetweet(int postId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _retweetService.ToggleRetweetAsync(userId, postId);
                return Ok(new { retweeted = result });
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error toggling retweet: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }
}
