using Microsoft.EntityFrameworkCore;
using XCloneAPI.Data;
using XCloneAPI.DTOs;
using XCloneAPI.Models;

namespace XCloneAPI.Services
{
    public class PostService : IPostService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PostService> _logger;

        public PostService(AppDbContext context, ILogger<PostService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // One row of a timeline: either an original post, or a retweet of a post by RetweeterId.
        private sealed class TimelineEntry
        {
            public int PostId { get; set; }
            public DateTime At { get; set; }
            public int? RetweeterId { get; set; }
        }

        public async Task<PostResponse> CreatePostAsync(int userId, PostRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Content))
                    throw new ArgumentException("Post content cannot be empty");

                var post = new Post
                {
                    UserId = userId,
                    Content = request.Content,
                    MediaUrls = request.MediaUrls ?? Array.Empty<string>()
                };

                _context.Posts.Add(post);
                await _context.SaveChangesAsync();

                var user = await _context.Users.FindAsync(userId);
                _logger.LogInformation($"Post created by user {userId}");

                return MapToPostResponse(post, user, isLiked: false, isRetweeted: false);
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating post: {ex.Message}");
                throw;
            }
        }

        public async Task<PostResponse?> CreateReplyAsync(int userId, int parentPostId, PostRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Content))
                    throw new ArgumentException("Post content cannot be empty");

                var parent = await _context.Posts.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == parentPostId);
                if (parent == null)
                    return null;

                var reply = new Post
                {
                    UserId = userId,
                    Content = request.Content,
                    MediaUrls = request.MediaUrls ?? Array.Empty<string>(),
                    ParentPostId = parentPostId
                };

                parent.RepliesCount++;
                _context.Posts.Add(reply);
                await _context.SaveChangesAsync();

                var user = await _context.Users.FindAsync(userId);
                _logger.LogInformation($"User {userId} replied to post {parentPostId}");

                var response = MapToPostResponse(reply, user, isLiked: false, isRetweeted: false);
                response.ReplyToUsername = parent.User.Username;
                return response;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating reply: {ex.Message}");
                throw;
            }
        }

        public async Task<PostResponse?> GetPostByIdAsync(int postId, int currentUserId)
        {
            try
            {
                var entries = new List<TimelineEntry>();
                if (await _context.Posts.AnyAsync(p => p.Id == postId))
                    entries.Add(new TimelineEntry { PostId = postId });

                var responses = await BuildResponsesAsync(entries, currentUserId);
                return responses.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching post: {ex.Message}");
                throw;
            }
        }

        // Home timeline: top-level posts and retweets from people the user follows, plus their own.
        public async Task<List<PostResponse>> GetFeedAsync(int userId, int skip, int take)
        {
            try
            {
                var authorIds = _context.Follows
                    .Where(f => f.FollowerId == userId)
                    .Select(f => f.FollowingId)
                    .Concat(_context.Users.Where(u => u.Id == userId).Select(u => u.Id));

                return await GetTimelineAsync(authorIds, userId, skip, take);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching feed: {ex.Message}");
                throw;
            }
        }

        // Profile "Posts" tab: a user's top-level posts and their retweets.
        public async Task<List<PostResponse>> GetUserPostsAsync(int userId, int currentUserId, int skip, int take)
        {
            try
            {
                var authorIds = _context.Users.Where(u => u.Id == userId).Select(u => u.Id);
                return await GetTimelineAsync(authorIds, currentUserId, skip, take);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching user posts: {ex.Message}");
                throw;
            }
        }

        // Profile "Replies" tab: replies written by the user, newest first.
        public async Task<List<PostResponse>> GetUserRepliesAsync(int userId, int currentUserId, int skip, int take)
        {
            try
            {
                var entries = await _context.Posts
                    .Where(p => p.UserId == userId && p.ParentPostId != null)
                    .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
                    .Skip(skip).Take(take)
                    .Select(p => new TimelineEntry { PostId = p.Id, At = p.CreatedAt })
                    .ToListAsync();

                return await BuildResponsesAsync(entries, currentUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching user replies: {ex.Message}");
                throw;
            }
        }

        // Conversation view: direct replies to a post, oldest first.
        public async Task<List<PostResponse>> GetRepliesAsync(int postId, int currentUserId, int skip, int take)
        {
            try
            {
                var entries = await _context.Posts
                    .Where(p => p.ParentPostId == postId)
                    .OrderBy(p => p.CreatedAt).ThenBy(p => p.Id)
                    .Skip(skip).Take(take)
                    .Select(p => new TimelineEntry { PostId = p.Id, At = p.CreatedAt })
                    .ToListAsync();

                return await BuildResponsesAsync(entries, currentUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching replies: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> DeletePostAsync(int postId, int userId)
        {
            try
            {
                var post = await _context.Posts.FindAsync(postId);
                if (post == null || post.UserId != userId)
                    return false;

                // Replies, retweets and likes of this post are removed by the database (cascade).
                if (post.ParentPostId != null)
                {
                    var parent = await _context.Posts.FindAsync(post.ParentPostId);
                    if (parent != null && parent.RepliesCount > 0)
                        parent.RepliesCount--;
                }

                _context.Posts.Remove(post);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Post {postId} deleted by user {userId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting post: {ex.Message}");
                throw;
            }
        }

        // Pages one merged stream of original posts and retweets (newest first) for the given authors.
        private async Task<List<PostResponse>> GetTimelineAsync(IQueryable<int> authorIds, int currentUserId, int skip, int take)
        {
            var originals = _context.Posts
                .Where(p => p.ParentPostId == null && authorIds.Contains(p.UserId))
                .Select(p => new TimelineEntry { PostId = p.Id, At = p.CreatedAt, RetweeterId = null });

            var retweets = _context.Retweets
                .Where(r => authorIds.Contains(r.UserId))
                .Select(r => new TimelineEntry { PostId = r.PostId, At = r.CreatedAt, RetweeterId = r.UserId });

            var entries = await originals.Concat(retweets)
                .OrderByDescending(e => e.At).ThenByDescending(e => e.PostId)
                .Skip(skip).Take(take)
                .ToListAsync();

            return await BuildResponsesAsync(entries, currentUserId);
        }

        // Loads everything for a page of entries in a fixed number of queries (no per-post round trips).
        private async Task<List<PostResponse>> BuildResponsesAsync(List<TimelineEntry> entries, int currentUserId)
        {
            if (entries.Count == 0)
                return new List<PostResponse>();

            var postIds = entries.Select(e => e.PostId).Distinct().ToList();
            var retweeterIds = entries.Where(e => e.RetweeterId != null).Select(e => e.RetweeterId!.Value).Distinct().ToList();

            var posts = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.ParentPost).ThenInclude(pp => pp!.User)
                .Where(p => postIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            var likedIds = (await _context.Likes
                .Where(l => l.UserId == currentUserId && postIds.Contains(l.PostId))
                .Select(l => l.PostId)
                .ToListAsync()).ToHashSet();

            var retweetedIds = (await _context.Retweets
                .Where(r => r.UserId == currentUserId && postIds.Contains(r.PostId))
                .Select(r => r.PostId)
                .ToListAsync()).ToHashSet();

            var retweeters = retweeterIds.Count == 0
                ? new Dictionary<int, User>()
                : await _context.Users.Where(u => retweeterIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id);

            var responses = new List<PostResponse>();
            foreach (var entry in entries)
            {
                if (!posts.TryGetValue(entry.PostId, out var post))
                    continue;

                var response = MapToPostResponse(post, post.User, likedIds.Contains(post.Id), retweetedIds.Contains(post.Id));
                response.ReplyToUsername = post.ParentPost?.User?.Username;
                if (entry.RetweeterId != null && retweeters.TryGetValue(entry.RetweeterId.Value, out var retweeter))
                    response.RetweetedBy = MapToUserResponse(retweeter);

                responses.Add(response);
            }

            return responses;
        }

        private static UserResponse MapToUserResponse(User user)
        {
            return new UserResponse
            {
                Id = user.Id,
                Username = user.Username,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl
            };
        }

        private PostResponse MapToPostResponse(Post post, User user, bool isLiked, bool isRetweeted)
        {
            return new PostResponse
            {
                Id = post.Id,
                UserId = post.UserId,
                Content = post.Content,
                MediaUrls = post.MediaUrls,
                LikesCount = post.LikesCount,
                RetweetsCount = post.RetweetsCount,
                RepliesCount = post.RepliesCount,
                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt,
                IsLiked = isLiked,
                IsRetweeted = isRetweeted,
                ParentPostId = post.ParentPostId,
                User = MapToUserResponse(user)
            };
        }
    }
}
