using XCloneAPI.DTOs;

namespace XCloneAPI.Services
{
    public interface IPostService
    {
        Task<PostResponse> CreatePostAsync(int userId, PostRequest request);
        Task<PostResponse?> CreateReplyAsync(int userId, int parentPostId, PostRequest request);
        Task<PostResponse?> GetPostByIdAsync(int postId, int currentUserId);
        Task<PagedResponse<PostResponse>> GetRepliesAsync(int postId, int currentUserId, int? afterId, int take);
        Task<PagedResponse<PostResponse>> GetUserRepliesAsync(int userId, int currentUserId, int? beforeId, int take);
        Task<PagedResponse<PostResponse>> GetFeedAsync(int userId, TimelineCursor? after, int take);
        Task<PagedResponse<PostResponse>> GetUserPostsAsync(int userId, int currentUserId, TimelineCursor? after, int take);
        Task<PagedResponse<PostResponse>> GetHashtagPostsAsync(string tag, int currentUserId, int? beforeId, int take);
        Task<bool> DeletePostAsync(int postId, int userId);
    }
}
