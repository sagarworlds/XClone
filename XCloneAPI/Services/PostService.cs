using Microsoft.EntityFrameworkCore;
using XCloneAPI.Data;
using XCloneAPI.DTOs;
using XCloneAPI.Models;

namespace XCloneAPI.Services
{
    public class PostService : IPostService
    {
        private readonly AppDbContext _context;
        private readonly IMediaStorage _media;
        private readonly ILogger<PostService> _logger;

        public PostService(AppDbContext context, IMediaStorage media, ILogger<PostService> logger)
        {
            _context = context;
            _media = media;
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
                EnsureImagesExist(request.MediaUrls);

                var post = new Post
                {
                    UserId = userId,
                    Content = request.Content,
                    MediaUrls = request.MediaUrls ?? Array.Empty<string>(),
                    Hashtags = HashtagsOf(request.Content)
                };

                // Name the people the text mentions, and tell them (not yourself), all in the same save as the post
                var mentioned = await ResolveMentionsAsync(request.Content);
                post.Mentions = mentioned.Select(u => new PostMention { UserId = u.Id }).ToList();
                AddMentionNotifications(post, userId, mentioned, alreadyNotified: null);

                _context.Posts.Add(post);
                await _context.SaveChangesAsync();

                var user = await _context.Users.FindAsync(userId);
                _logger.LogInformation($"Post created by user {userId}");

                var response = MapToPostResponse(post, user, isLiked: false, isRetweeted: false);
                response.Mentions = MentionNames(mentioned);
                return response;
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
                EnsureImagesExist(request.MediaUrls);

                var parent = await _context.Posts.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == parentPostId);
                if (parent == null)
                    return null;

                var reply = new Post
                {
                    UserId = userId,
                    Content = request.Content,
                    MediaUrls = request.MediaUrls ?? Array.Empty<string>(),
                    ParentPostId = parentPostId,
                    Hashtags = HashtagsOf(request.Content)
                };

                var mentioned = await ResolveMentionsAsync(request.Content);
                reply.Mentions = mentioned.Select(u => new PostMention { UserId = u.Id }).ToList();

                parent.RepliesCount++;
                _context.Posts.Add(reply);

                // Tell the author of the post being replied to (not when replying to yourself). The notification
                // points at the reply, and is saved together with it.
                if (parent.UserId != userId)
                {
                    _context.Notifications.Add(new Notification
                    {
                        RecipientId = parent.UserId,
                        ActorId = userId,
                        Type = NotificationType.Reply,
                        Post = reply
                    });
                }

                // The author of the post being replied to is already told about the reply itself
                AddMentionNotifications(reply, userId, mentioned, alreadyNotified: parent.UserId);

                await _context.SaveChangesAsync();

                var user = await _context.Users.FindAsync(userId);
                _logger.LogInformation($"User {userId} replied to post {parentPostId}");

                var response = MapToPostResponse(reply, user, isLiked: false, isRetweeted: false);
                response.ReplyToUsername = parent.User.Username;
                response.Mentions = MentionNames(mentioned);
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
        public async Task<PagedResponse<PostResponse>> GetFeedAsync(int userId, TimelineCursor? after, int take)
        {
            try
            {
                var authorIds = _context.Follows
                    .Where(f => f.FollowerId == userId)
                    .Select(f => f.FollowingId)
                    .Concat(_context.Users.Where(u => u.Id == userId).Select(u => u.Id));

                return await GetTimelineAsync(authorIds, userId, after, take);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching feed: {ex.Message}");
                throw;
            }
        }

        // Profile "Posts" tab: a user's top-level posts and their retweets.
        public async Task<PagedResponse<PostResponse>> GetUserPostsAsync(int userId, int currentUserId, TimelineCursor? after, int take)
        {
            try
            {
                var authorIds = _context.Users.Where(u => u.Id == userId).Select(u => u.Id);
                return await GetTimelineAsync(authorIds, currentUserId, after, take);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching user posts: {ex.Message}");
                throw;
            }
        }

        // Profile "Replies" tab: replies written by the user, newest first (ids only ever grow, so id order is time order).
        public async Task<PagedResponse<PostResponse>> GetUserRepliesAsync(int userId, int currentUserId, int? beforeId, int take)
        {
            try
            {
                var query = _context.Posts.Where(p => p.UserId == userId && p.ParentPostId != null);
                if (beforeId != null)
                    query = query.Where(p => p.Id < beforeId);

                // One more than asked for tells whether there is a next page
                var entries = await query
                    .OrderByDescending(p => p.Id)
                    .Take(take + 1)
                    .Select(p => new TimelineEntry { PostId = p.Id, At = p.CreatedAt })
                    .ToListAsync();

                return await BuildPageAsync(entries, take, currentUserId, e => IdCursor.Encode(e.PostId));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching user replies: {ex.Message}");
                throw;
            }
        }

        // Conversation view: direct replies to a post, oldest first.
        public async Task<PagedResponse<PostResponse>> GetRepliesAsync(int postId, int currentUserId, int? afterId, int take)
        {
            try
            {
                var query = _context.Posts.Where(p => p.ParentPostId == postId);
                if (afterId != null)
                    query = query.Where(p => p.Id > afterId);

                var entries = await query
                    .OrderBy(p => p.Id)
                    .Take(take + 1)
                    .Select(p => new TimelineEntry { PostId = p.Id, At = p.CreatedAt })
                    .ToListAsync();

                return await BuildPageAsync(entries, take, currentUserId, e => IdCursor.Encode(e.PostId));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching replies: {ex.Message}");
                throw;
            }
        }

        // Every post (and reply) that has the hashtag, newest first. The tag is compared in lower case.
        public async Task<PagedResponse<PostResponse>> GetHashtagPostsAsync(string tag, int currentUserId, int? beforeId, int take)
        {
            try
            {
                var query = _context.PostHashtags.Where(h => h.Tag == tag);
                if (beforeId != null)
                    query = query.Where(h => h.PostId < beforeId);

                var entries = await query
                    .OrderByDescending(h => h.PostId)
                    .Take(take + 1)
                    .Select(h => new TimelineEntry { PostId = h.PostId })
                    .ToListAsync();

                return await BuildPageAsync(entries, take, currentUserId, e => IdCursor.Encode(e.PostId));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching hashtag posts: {ex.Message}");
                throw;
            }
        }

        // The users a text names with @ that exist, at most 10. A name matches without regard to case; when two
        // accounts differ only by case (older accounts), the one spelled exactly as written wins.
        private async Task<List<User>> ResolveMentionsAsync(string content)
        {
            var names = MentionParser.Parse(content);
            if (names.Count == 0)
                return new List<User>();

            var lowered = names.Select(n => n.ToLowerInvariant()).ToList();
            var candidates = await _context.Users
                .Where(u => lowered.Contains(u.Username.ToLower()))
                .OrderBy(u => u.Id)
                .ToListAsync();

            var resolved = new List<User>();
            foreach (var name in names)
            {
                var matches = candidates.Where(u => string.Equals(u.Username, name, StringComparison.OrdinalIgnoreCase)).ToList();
                var user = matches.FirstOrDefault(u => u.Username == name) ?? matches.FirstOrDefault();
                if (user != null && !resolved.Contains(user))
                    resolved.Add(user);
            }

            return resolved;
        }

        // Everyone named in the post is told, except the author and the one who is told about the post anyway
        private void AddMentionNotifications(Post post, int authorId, List<User> mentioned, int? alreadyNotified)
        {
            foreach (var user in mentioned.Where(u => u.Id != authorId && u.Id != alreadyNotified))
            {
                _context.Notifications.Add(new Notification
                {
                    RecipientId = user.Id,
                    ActorId = authorId,
                    Type = NotificationType.Mention,
                    Post = post
                });
            }
        }

        private static string[] MentionNames(IEnumerable<User> users) =>
            users.Select(u => u.Username).OrderBy(n => n, StringComparer.Ordinal).ToArray();

        // The rows that record a post's hashtags (saved together with the post)
        private static List<PostHashtag> HashtagsOf(string content) =>
            HashtagParser.Parse(content).Select(tag => new PostHashtag { Tag = tag }).ToList();

        // Changes the text of one of your own posts (null when there is no such post or it is not yours). The hashtags
        // and mentions are worked out again: their rows follow the new text, and only people who are named now and
        // were not before are told. Someone taken out of the text loses the notification about it. Saying the same
        // thing again changes nothing (no "edited" mark).
        public async Task<PostResponse?> UpdatePostAsync(int postId, int userId, UpdatePostRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Content))
                    throw new ArgumentException("Post content cannot be empty");

                var post = await _context.Posts
                    .Include(p => p.Hashtags)
                    .Include(p => p.Mentions)
                    .FirstOrDefaultAsync(p => p.Id == postId);
                if (post == null || post.UserId != userId)
                    return null;

                if (request.Content == post.Content)
                    return await GetPostByIdAsync(postId, userId);

                // The database keeps microseconds, so the answer carries exactly what a later read will show
                var now = DateTime.UtcNow;
                now = new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc);
                post.Content = request.Content;
                post.UpdatedAt = now;
                post.EditedAt = now;

                var tags = HashtagParser.Parse(request.Content);
                _context.PostHashtags.RemoveRange(post.Hashtags.Where(h => !tags.Contains(h.Tag)).ToList());
                foreach (var tag in tags.Where(t => post.Hashtags.All(h => h.Tag != t)))
                    post.Hashtags.Add(new PostHashtag { Tag = tag });

                var mentioned = await ResolveMentionsAsync(request.Content);
                var mentionedIds = mentioned.Select(u => u.Id).ToHashSet();
                var alreadyMentioned = post.Mentions.Select(m => m.UserId).ToHashSet();

                var dropped = post.Mentions.Where(m => !mentionedIds.Contains(m.UserId)).ToList();
                if (dropped.Count > 0)
                {
                    _context.PostMentions.RemoveRange(dropped);
                    var droppedIds = dropped.Select(m => m.UserId).ToList();
                    _context.Notifications.RemoveRange(await _context.Notifications
                        .Where(n => n.PostId == postId && n.Type == NotificationType.Mention && droppedIds.Contains(n.RecipientId))
                        .ToListAsync());
                }

                var added = mentioned.Where(u => !alreadyMentioned.Contains(u.Id)).ToList();
                foreach (var user in added)
                    post.Mentions.Add(new PostMention { UserId = user.Id });

                // The author of the post a reply answers is told about the reply anyway
                int? repliedTo = post.ParentPostId == null
                    ? null
                    : await _context.Posts.Where(p => p.Id == post.ParentPostId).Select(p => (int?)p.UserId).FirstOrDefaultAsync();
                AddMentionNotifications(post, userId, added, alreadyNotified: repliedTo);

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Post {postId} edited by user {userId}");

                return await GetPostByIdAsync(postId, userId);
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error editing post: {ex.Message}");
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

                // Replies, retweets and likes of this post are removed by the database (cascade), and so are the
                // images of the post and of all its replies, once the delete has succeeded.
                var imageNames = await ImageNamesOfThreadAsync(post);

                if (post.ParentPostId != null)
                {
                    var parent = await _context.Posts.FindAsync(post.ParentPostId);
                    if (parent != null && parent.RepliesCount > 0)
                        parent.RepliesCount--;
                }

                _context.Posts.Remove(post);
                await _context.SaveChangesAsync();
                DeleteImages(imageNames);

                _logger.LogInformation($"Post {postId} deleted by user {userId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting post: {ex.Message}");
                throw;
            }
        }

        // A post can only carry images that were uploaded here and are still there
        private void EnsureImagesExist(string[]? mediaUrls)
        {
            foreach (var url in mediaUrls ?? Array.Empty<string>())
            {
                if (!MediaNames.TryGetName(url, out var name) || !_media.Exists(name))
                    throw new ArgumentException("An attached image was not found. Upload it again.");
            }
        }

        // The uploaded images of a post and of every reply below it (they all go when the post is deleted)
        private async Task<List<string>> ImageNamesOfThreadAsync(Post post)
        {
            var names = new List<string>();
            AddImageNames(names, post.MediaUrls);

            var level = new List<int> { post.Id };
            while (level.Count > 0)
            {
                var replies = await _context.Posts
                    .Where(p => p.ParentPostId != null && level.Contains(p.ParentPostId.Value))
                    .Select(p => new { p.Id, p.MediaUrls })
                    .ToListAsync();

                foreach (var reply in replies)
                    AddImageNames(names, reply.MediaUrls);

                level = replies.Select(r => r.Id).ToList();
            }

            return names;
        }

        private static void AddImageNames(List<string> names, string[]? mediaUrls)
        {
            foreach (var url in mediaUrls ?? Array.Empty<string>())
            {
                if (MediaNames.TryGetName(url, out var name))
                    names.Add(name);
            }
        }

        // The post is already gone, so a file that cannot be removed is only logged (it is just clutter)
        private void DeleteImages(List<string> names)
        {
            foreach (var name in names.Distinct())
            {
                try
                {
                    _media.Delete(name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Could not remove image {name}: {ex.Message}");
                }
            }
        }

        // Pages one merged stream of original posts and retweets (newest first) for the given authors. An entry is
        // identified by (moment, post, who reposted it - 0 for the original), and a page starts right after the entry
        // the cursor names.
        private async Task<PagedResponse<PostResponse>> GetTimelineAsync(IQueryable<int> authorIds, int currentUserId, TimelineCursor? after, int take)
        {
            var originals = _context.Posts
                .Where(p => p.ParentPostId == null && authorIds.Contains(p.UserId))
                .Select(p => new TimelineEntry { PostId = p.Id, At = p.CreatedAt, RetweeterId = null });

            var retweets = _context.Retweets
                .Where(r => authorIds.Contains(r.UserId))
                .Select(r => new TimelineEntry { PostId = r.PostId, At = r.CreatedAt, RetweeterId = r.UserId });

            var stream = originals.Concat(retweets);
            if (after != null)
            {
                var at = after.At;
                var postId = after.PostId;
                var retweeterId = after.RetweeterId;
                stream = stream.Where(e => e.At < at
                    || (e.At == at && (e.PostId < postId
                        || (e.PostId == postId && (e.RetweeterId ?? 0) < retweeterId))));
            }

            var entries = await stream
                .OrderByDescending(e => e.At).ThenByDescending(e => e.PostId).ThenByDescending(e => e.RetweeterId ?? 0)
                .Take(take + 1)
                .ToListAsync();

            return await BuildPageAsync(entries, take, currentUserId,
                e => new TimelineCursor(e.At, e.PostId, e.RetweeterId ?? 0).Encode());
        }

        // Turns the rows of a query that asked for one more than the page size into a page: the extra row is not
        // shown, it only proves there is more, and the cursor points at the last row that is.
        private async Task<PagedResponse<PostResponse>> BuildPageAsync(List<TimelineEntry> rows, int take, int currentUserId, Func<TimelineEntry, string> cursorOf)
        {
            var hasMore = rows.Count > take;
            var page = hasMore ? rows.Take(take).ToList() : rows;

            return new PagedResponse<PostResponse>
            {
                Items = await BuildResponsesAsync(page, currentUserId),
                NextCursor = hasMore ? cursorOf(page[^1]) : null
            };
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

            var mentions = (await _context.PostMentions
                    .Where(m => postIds.Contains(m.PostId))
                    .Select(m => new { m.PostId, m.User.Username })
                    .ToListAsync())
                .GroupBy(m => m.PostId)
                .ToDictionary(g => g.Key, g => g.Select(m => m.Username).OrderBy(n => n, StringComparer.Ordinal).ToArray());

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
                response.Mentions = mentions.GetValueOrDefault(post.Id, Array.Empty<string>());
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
                EditedAt = post.EditedAt,
                IsLiked = isLiked,
                IsRetweeted = isRetweeted,
                ParentPostId = post.ParentPostId,
                User = MapToUserResponse(user)
            };
        }
    }
}
