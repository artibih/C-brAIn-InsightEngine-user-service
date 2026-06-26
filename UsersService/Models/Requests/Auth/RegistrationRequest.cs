using System.ComponentModel.DataAnnotations;

namespace UsersService.Models.Requests.Auth
{
    public class RegistrationRequest
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Last name is required.")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Role ID is required.")]
        public string RoleId { get; set; }
        public int? OrganizationId { get; set; }
        public string? OrganizationName { get; set; }
        public string? Justification { get; set; }

        /// <summary>EULA version the user agreed to during registration (optional).</summary>
        public string? AcceptedEulaVersion { get; set; }
    }
}
