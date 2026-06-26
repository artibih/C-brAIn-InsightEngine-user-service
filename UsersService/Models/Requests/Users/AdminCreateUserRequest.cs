using System.ComponentModel.DataAnnotations;

namespace UsersService.Models.Requests.Users
{
    public class AdminCreateUserRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        [Required]
        public string FirstName { get; set; }
        [Required]
        public string LastName { get; set; }
    }
}
