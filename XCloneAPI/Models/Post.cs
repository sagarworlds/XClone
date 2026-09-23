using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace XCloneAPI.Models
{
    [Table("posts")]
    public class Post
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        [Required]
        [Column("content")]
        public string Content { get; set; }

        [Column("media_urls")]
        public string[] MediaUrls { get; set; }

        [Column("likes_count")]
        public int LikesCount { get; set; } = 0;

        [Column("retweets_count")]
        public int RetweetsCount { get; set; } = 0;

        [Column("replies_count")]
        public int RepliesCount { get; set; } = 0;

        // Set when this post is a reply; null for top-level posts
        [Column("parent_post_id")]
        public int? ParentPostId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // When the author last changed the text; null for a post that was never edited
        [Column("edited_at")]
        public DateTime? EditedAt { get; set; }

        // Navigation Properties
        [ForeignKey("UserId")]
        public User User { get; set; }

        public Post? ParentPost { get; set; }

        public ICollection<Post> Replies { get; set; } = new List<Post>();
        public ICollection<Like> Likes { get; set; } = new List<Like>();
        public ICollection<Retweet> Retweets { get; set; } = new List<Retweet>();
        public ICollection<PostHashtag> Hashtags { get; set; } = new List<PostHashtag>();
        public ICollection<PostMention> Mentions { get; set; } = new List<PostMention>();
    }
}
