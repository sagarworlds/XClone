using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace XCloneAPI.Models
{
    // One hashtag of one post (lower case, without the #). Written together with the post; removed with it.
    [Table("post_hashtags")]
    public class PostHashtag
    {
        [Column("post_id")]
        public int PostId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("tag")]
        public string Tag { get; set; } = "";

        [ForeignKey("PostId")]
        public Post Post { get; set; } = null!;
    }
}
