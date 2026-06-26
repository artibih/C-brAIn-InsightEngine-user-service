using System.ComponentModel.DataAnnotations;

namespace UsersService.Models.Requests.Auth
{
    public class LoginRequest
    {
        [EmailAddress]
        [Required]
        public string EmailAddress { get; set; }
        [Required]
        public string Password { get; set; }
        public bool IsPersistent { get; set; }
    }
}
