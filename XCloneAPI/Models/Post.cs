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

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("UserId")]
        public User User { get; set; }

        public ICollection<Like> Likes { get; set; } = new List<Like>();
    }
}
