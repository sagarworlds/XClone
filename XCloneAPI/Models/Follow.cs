using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace XCloneAPI.Models
{
    [Table("follows")]
    public class Follow
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("follower_id")]
        public int FollowerId { get; set; }

        [Required]
        [Column("following_id")]
        public int FollowingId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("FollowerId")]
        public User Follower { get; set; }

        [ForeignKey("FollowingId")]
        public User Following { get; set; }
    }
}
