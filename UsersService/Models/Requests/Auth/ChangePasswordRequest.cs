using System.ComponentModel.DataAnnotations;

namespace UsersService.Models.Requests.Auth
{
    public class ChangePasswordRequest
    {
        [EmailAddress]
        public string Email { get; set; }
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
    }
}
