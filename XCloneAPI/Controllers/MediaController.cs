using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using XCloneAPI.Services;

namespace XCloneAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [EnableRateLimiting("upload")]
    public class MediaController : ControllerBase
    {
        public const long MaxImageBytes = 5 * 1024 * 1024;

        // The whole request may be a little larger than the image (multipart framing)
        private const long MaxRequestBytes = MaxImageBytes + 512 * 1024;

        private readonly IMediaStorage _storage;
        private readonly ILogger<MediaController> _logger;

        public MediaController(IMediaStorage storage, ILogger<MediaController> logger)
        {
            _storage = storage;
            _logger = logger;
        }

        // Uploads one image (multipart form, field "file") and answers with the address to attach to a post.
        [HttpPost]
        [RequestSizeLimit(MaxRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
        public async Task<ActionResult<MediaResponse>> Upload(IFormFile? file, CancellationToken cancellationToken)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest(new { message = "Choose an image to upload." });

                if (file.Length > MaxImageBytes)
                    return BadRequest(new { message = "Images can be at most 5 MB." });

                await using var content = file.OpenReadStream();

                // The kind comes from what the file is, never from what the client says it is
                var header = new byte[MediaNames.HeaderLength];
                var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
                var kind = MediaNames.Detect(header.AsSpan(0, read));
                if (kind == null)
                    return BadRequest(new { message = "Only PNG, JPEG, GIF and WebP images can be uploaded." });

                content.Position = 0;
                var name = MediaNames.NewName(kind.Value);
                await _storage.SaveAsync(name, content, cancellationToken);

                return Ok(new MediaResponse { Url = MediaNames.ToUrl(name) });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError($"Error uploading image: {ex.Message}");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }

    public class MediaResponse
    {
        // Attach this to a post's mediaUrls; the image is served from it
        public string Url { get; set; } = "";
    }
}
