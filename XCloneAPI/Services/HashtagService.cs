using Microsoft.EntityFrameworkCore;
using XCloneAPI.Data;
using XCloneAPI.DTOs;

namespace XCloneAPI.Services
{
    public interface IHashtagService
    {
        Task<List<TrendingHashtagResponse>> GetTrendingAsync(int take);
    }

    public class HashtagService : IHashtagService
    {
        // Tags are trending by how many posts used them in the last week
        public static readonly TimeSpan TrendingWindow = TimeSpan.FromDays(7);

        private readonly AppDbContext _context;
        private readonly ILogger<HashtagService> _logger;

        public HashtagService(AppDbContext context, ILogger<HashtagService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // The most used tags of the last week, most used first (ties in alphabetical order, so the order is stable)
        public async Task<List<TrendingHashtagResponse>> GetTrendingAsync(int take)
        {
            try
            {
                var since = DateTime.UtcNow - TrendingWindow;

                return await _context.PostHashtags
                    .Where(h => h.Post.CreatedAt >= since)
                    .GroupBy(h => h.Tag)
                    .Select(g => new TrendingHashtagResponse { Tag = g.Key, PostsCount = g.Count() })
                    .OrderByDescending(t => t.PostsCount)
                    .ThenBy(t => t.Tag)
                    .Take(take)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching trending hashtags: {ex.Message}");
                throw;
            }
        }
    }
}
