using System.ComponentModel.DataAnnotations;

namespace XCloneAPI.DTOs
{
    public class UpdateProfileRequest
    {
        [StringLength(100)]
        public string DisplayName { get; set; }

        [StringLength(500)]
        public string Bio { get; set; }

        [StringLength(500)]
        public string AvatarUrl { get; set; }
    }
}