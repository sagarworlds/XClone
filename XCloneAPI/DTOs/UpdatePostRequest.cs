using System.ComponentModel.DataAnnotations;

namespace XCloneAPI.DTOs
{
    // Editing a post changes its text only: the images (and later the post it quotes) stay as they were
    public class UpdatePostRequest
    {
        [Required]
        [StringLength(280)]
        public string Content { get; set; }
    }
}
