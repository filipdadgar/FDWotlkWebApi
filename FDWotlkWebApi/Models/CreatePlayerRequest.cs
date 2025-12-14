using System.ComponentModel.DataAnnotations;

namespace FDWotlkWebApi.Models
{
    public class CreatePlayerRequest
    {
        [Required]
        [StringLength(32, MinimumLength = 3)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;
        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string PasswordRepeat { get; set; } = string.Empty;
    }
}
