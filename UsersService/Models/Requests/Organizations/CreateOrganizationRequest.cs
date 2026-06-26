using System.ComponentModel.DataAnnotations;

namespace UsersService.Models.Requests.Organizations
{
    public class CreateOrganizationRequest
    {
        [Required(ErrorMessage = "Organization name is required.")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Description is required.")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Address is required.")]
        public string Address { get; set; }

        [Required(ErrorMessage = "City is required.")]
        public string City { get; set; }

        [Required(ErrorMessage = "Country is required.")]
        public string Country { get; set; }

        [Required(ErrorMessage = "Phone number is required.")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "Organization email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Website is required.")]
        public string Website { get; set; }
        public string Logo { get; set; }
        public bool ShouldSendEmail { get; set; } = true;

        [Required(ErrorMessage = "Admin email is required.")]
        [EmailAddress(ErrorMessage = "Invalid admin email format.")]
        public string AdminEmail { get; set; }

        [Required(ErrorMessage = "Admin first name is required.")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Admin last name is required.")]
        public string LastName { get; set; }
    }
}
