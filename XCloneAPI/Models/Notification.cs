using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace XCloneAPI.Models
{
    public enum NotificationType
    {
        // Someone replied to one of your posts; PostId is the reply
        Reply,
        // Someone reposted one of your posts; PostId is the post that was reposted
        Repost
    }

    [Table("notifications")]
    public class Notification
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        // Who is told about it
        [Required]
        [Column("recipient_id")]
        public int RecipientId { get; set; }

        // Who did it
        [Required]
        [Column("actor_id")]
        public int ActorId { get; set; }

        [Required]
        [Column("type")]
        public NotificationType Type { get; set; }

        [Required]
        [Column("post_id")]
        public int PostId { get; set; }

        [Column("is_read")]
        public bool IsRead { get; set; } = false;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("RecipientId")]
        public User Recipient { get; set; }

        [ForeignKey("ActorId")]
        public User Actor { get; set; }

        [ForeignKey("PostId")]
        public Post Post { get; set; }
    }
}
