using System.ComponentModel.DataAnnotations;

namespace XCloneAPI.DTOs
{
    public class PostRequest
    {
        [Required]
        [StringLength(280)]
        public string Content { get; set; }

        public string[] MediaUrls { get; set; }
    }
}