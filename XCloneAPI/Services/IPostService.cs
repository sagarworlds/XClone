using XCloneAPI.DTOs;

namespace XCloneAPI.Services
{
    public interface IPostService
    {
        Task<PostResponse> CreatePostAsync(int userId, PostRequest request);
        Task<PostResponse?> CreateReplyAsync(int userId, int parentPostId, PostRequest request);
        Task<PostResponse?> GetPostByIdAsync(int postId, int currentUserId);
        Task<List<PostResponse>> GetRepliesAsync(int postId, int currentUserId, int skip, int take);
        Task<List<PostResponse>> GetUserRepliesAsync(int userId, int currentUserId, int skip, int take);
        Task<List<PostResponse>> GetFeedAsync(int userId, int skip, int take);
        Task<List<PostResponse>> GetUserPostsAsync(int userId, int currentUserId, int skip, int take);
        Task<bool> DeletePostAsync(int postId, int userId);
    }
}
