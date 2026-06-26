using System.ComponentModel.DataAnnotations;

namespace UsersService.Models.Requests.Users
{
    public class CreateUserRequest
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Last name is required.")]
        public string LastName { get; set; }

        public int? OrganizationId { get; set; }
    }
}
