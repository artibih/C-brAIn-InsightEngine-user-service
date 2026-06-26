using System.ComponentModel.DataAnnotations;

namespace UsersService.Models.Requests.Auth
{
    public class ForgotPasswordRequest
    {
        [Required]
        [EmailAddress]
        public string EmailAddress { get; set; }
    }

}
