using System.ComponentModel.DataAnnotations;

namespace UsersService.Models.Requests.Auth
{
    public class ResetPasswordRequest
    {
        [Required]
        public string UserId { get; set; }

        [Required]
        public string Token { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 8)]
        public string NewPassword { get; set; }
    }

}
