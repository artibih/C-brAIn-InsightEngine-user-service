using System.ComponentModel.DataAnnotations;

namespace UsersService.Models.Requests.Auth
{
    public class TokenRefreshRequest
    {
        [Required]
        public string RefreshToken { get; set; }
    }
}
