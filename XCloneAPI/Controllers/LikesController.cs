using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using XCloneAPI.Services;

namespace XCloneAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LikesController : ControllerBase
    {
        private readonly ILikeService _likeService;
        private readonly ILogger<LikesController> _logger;
        private const int MaxPageSize = 50;

        public LikesController(ILikeService likeService, ILogger<LikesController> logger)
        {
            _likeService = likeService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }

        [HttpPost("toggle/{postId}")]
        public async Task<IActionResult> ToggleLike(int postId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _likeService.ToggleLikeAsync(userId, postId);
                return Ok(new { liked = result });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error toggling like: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("post/{postId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPostLikes(int postId, [FromQuery] int skip = 0, [FromQuery] int take = 10)
        {
            try
            {
                skip = Math.Max(skip, 0);
                take = Math.Clamp(take, 1, MaxPageSize);
                var likes = await _likeService.GetPostLikesAsync(postId, skip, take);
                return Ok(likes);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching likes: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }
}
