using System.ComponentModel.DataAnnotations.Schema;

namespace XCloneAPI.Models
{
    // A user named ("@username") in a post. Written together with the post; removed with the post or the user.
    [Table("post_mentions")]
    public class PostMention
    {
        [Column("post_id")]
        public int PostId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [ForeignKey("PostId")]
        public Post Post { get; set; } = null!;

        [ForeignKey("UserId")]
        public User User { get; set; } = null!;
    }
}
