using System.ComponentModel.DataAnnotations;

namespace XCloneAPI.DTOs
{
    public class LoginRequest
    {
        [Required]
        public string UsernameOrEmail { get; set; }

        [Required]
        [StringLength(255, MinimumLength = 6)]
        public string Password { get; set; }
    }
}
