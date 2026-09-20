using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using XCloneAPI.DTOs;
using XCloneAPI.Services;

namespace XCloneAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PostsController : ControllerBase
    {
        private readonly IPostService _postService;
        private readonly ILogger<PostsController> _logger;
        private const int MaxPageSize = 50;
        private const string InvalidCursorMessage = "Invalid cursor";

        public PostsController(IPostService postService, ILogger<PostsController> logger)
        {
            _postService = postService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }

        [HttpPost]
        public async Task<ActionResult<PostResponse>> CreatePost([FromBody] PostRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var post = await _postService.CreatePostAsync(userId, request);
                return CreatedAtAction(nameof(GetPost), new { id = post.Id }, post);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating post: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<PostResponse>> GetPost(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var post = await _postService.GetPostByIdAsync(id, userId);
                if (post == null)
                    return NotFound(new { message = "Post not found" });
                return Ok(post);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching post: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("feed")]
        public async Task<ActionResult<PagedResponse<PostResponse>>> GetFeed([FromQuery] string? cursor = null, [FromQuery] int take = 10)
        {
            try
            {
                if (!TimelineCursor.TryParse(cursor, out var after))
                    return BadRequest(new { message = InvalidCursorMessage });

                take = Math.Clamp(take, 1, MaxPageSize);
                var userId = GetCurrentUserId();
                var posts = await _postService.GetFeedAsync(userId, after, take);
                return Ok(posts);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching feed: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("user/{userId}")]
        [AllowAnonymous]
        public async Task<ActionResult<PagedResponse<PostResponse>>> GetUserPosts(int userId, [FromQuery] string? cursor = null, [FromQuery] int take = 10)
        {
            try
            {
                if (!TimelineCursor.TryParse(cursor, out var after))
                    return BadRequest(new { message = InvalidCursorMessage });

                take = Math.Clamp(take, 1, MaxPageSize);
                var currentUserId = GetCurrentUserId();
                var posts = await _postService.GetUserPostsAsync(userId, currentUserId, after, take);
                return Ok(posts);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching user posts: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPost("{id:int}/replies")]
        public async Task<ActionResult<PostResponse>> CreateReply(int id, [FromBody] PostRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var reply = await _postService.CreateReplyAsync(userId, id, request);
                if (reply == null)
                    return NotFound(new { message = "Post not found" });
                return CreatedAtAction(nameof(GetPost), new { id = reply.Id }, reply);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating reply: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("{id:int}/replies")]
        [AllowAnonymous]
        public async Task<ActionResult<PagedResponse<PostResponse>>> GetReplies(int id, [FromQuery] string? cursor = null, [FromQuery] int take = 10)
        {
            try
            {
                if (!IdCursor.TryParse(cursor, out var afterId))
                    return BadRequest(new { message = InvalidCursorMessage });

                take = Math.Clamp(take, 1, MaxPageSize);
                var currentUserId = GetCurrentUserId();
                var replies = await _postService.GetRepliesAsync(id, currentUserId, afterId, take);
                return Ok(replies);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching replies: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("user/{userId:int}/replies")]
        [AllowAnonymous]
        public async Task<ActionResult<PagedResponse<PostResponse>>> GetUserReplies(int userId, [FromQuery] string? cursor = null, [FromQuery] int take = 10)
        {
            try
            {
                if (!IdCursor.TryParse(cursor, out var beforeId))
                    return BadRequest(new { message = InvalidCursorMessage });

                take = Math.Clamp(take, 1, MaxPageSize);
                var currentUserId = GetCurrentUserId();
                var replies = await _postService.GetUserRepliesAsync(userId, currentUserId, beforeId, take);
                return Ok(replies);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching user replies: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePost(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var success = await _postService.DeletePostAsync(id, userId);
                if (!success)
                    return NotFound(new { message = "Post not found or unauthorized" });
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting post: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }
}