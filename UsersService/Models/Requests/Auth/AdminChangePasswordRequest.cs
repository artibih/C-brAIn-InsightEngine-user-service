using System.ComponentModel.DataAnnotations;

namespace UsersService.Models.Requests.Auth
{
    public class AdminChangePasswordRequest
    {
        [Required]
        [EmailAddress]
        public string EmailAddress { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; }
    }

}
