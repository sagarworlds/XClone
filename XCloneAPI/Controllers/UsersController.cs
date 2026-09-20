using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using XCloneAPI.DTOs;
using XCloneAPI.Services;

namespace XCloneAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UsersController> _logger;
        private const int MaxPageSize = 50;

        public UsersController(IUserService userService, ILogger<UsersController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }

        [HttpGet("profile")]
        [Authorize]
        public async Task<ActionResult<UserResponse>> GetCurrentProfile()
        {
            try
            {
                var userId = GetCurrentUserId();
                var user = await _userService.GetUserByIdAsync(userId, userId);
                if (user == null)
                    return NotFound(new { message = "User not found" });
                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching current profile: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("profile/{username}")]
        [Authorize]
        public async Task<ActionResult<UserResponse>> GetProfileByUsername(string username)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var user = await _userService.GetUserByUsernameAsync(username, currentUserId);
                if (user == null)
                    return NotFound(new { message = "User not found" });
                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching profile by username: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("suggestions")]
        [Authorize]
        public async Task<ActionResult<List<UserResponse>>> GetSuggestions([FromQuery] int take = 4)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var users = await _userService.GetSuggestionsAsync(currentUserId, Math.Clamp(take, 1, 20));
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching suggestions: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<UserResponse>> GetUser(int id)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var user = await _userService.GetUserByIdAsync(id, currentUserId);
                if (user == null)
                    return NotFound(new { message = "User not found" });
                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching user: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<ActionResult<List<UserResponse>>> SearchUsers([FromQuery] string query, [FromQuery] int take = 10)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                    return BadRequest(new { message = "Search query is required" });

                take = Math.Clamp(take, 1, MaxPageSize);
                var currentUserId = GetCurrentUserId();
                var users = await _userService.SearchUsersAsync(query, currentUserId, take);
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error searching users: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpPut("profile")]
        [Authorize]
        public async Task<ActionResult<UserResponse>> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var user = await _userService.UpdateProfileAsync(userId, request);
                if (user == null)
                    return NotFound(new { message = "User not found" });
                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating profile: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("{id}/followers")]
        [AllowAnonymous]
        public async Task<ActionResult<List<UserResponse>>> GetFollowers(int id, [FromQuery] int skip = 0, [FromQuery] int take = 10)
        {
            try
            {
                skip = Math.Max(skip, 0);
                take = Math.Clamp(take, 1, MaxPageSize);
                var currentUserId = GetCurrentUserId();
                var followers = await _userService.GetFollowersAsync(id, currentUserId, skip, take);
                return Ok(followers);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching followers: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("{id}/following")]
        [AllowAnonymous]
        public async Task<ActionResult<List<UserResponse>>> GetFollowing(int id, [FromQuery] int skip = 0, [FromQuery] int take = 10)
        {
            try
            {
                skip = Math.Max(skip, 0);
                take = Math.Clamp(take, 1, MaxPageSize);
                var currentUserId = GetCurrentUserId();
                var following = await _userService.GetFollowingAsync(id, currentUserId, skip, take);
                return Ok(following);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching following: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }
}