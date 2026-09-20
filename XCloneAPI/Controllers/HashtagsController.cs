using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XCloneAPI.DTOs;
using XCloneAPI.Services;

namespace XCloneAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HashtagsController : ControllerBase
    {
        private const int MaxTrending = 20;

        private readonly IHashtagService _hashtagService;
        private readonly ILogger<HashtagsController> _logger;

        public HashtagsController(IHashtagService hashtagService, ILogger<HashtagsController> logger)
        {
            _hashtagService = hashtagService;
            _logger = logger;
        }

        // The tags most used by posts of the last week
        [HttpGet("trending")]
        [AllowAnonymous]
        public async Task<ActionResult<List<TrendingHashtagResponse>>> GetTrending([FromQuery] int take = 5)
        {
            try
            {
                return Ok(await _hashtagService.GetTrendingAsync(Math.Clamp(take, 1, MaxTrending)));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching trending hashtags: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }
}
