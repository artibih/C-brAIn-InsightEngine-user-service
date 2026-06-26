using Microsoft.AspNetCore.Identity;

namespace UsersService.Models.Entities
{
    public class ApplicationUser : IdentityUser<int>
    {
        public override int Id { get; set; }
        public override string UserName { get; set; }
        public override string Email { get; set; }
        public override string PasswordHash { get; set; }
        public override bool EmailConfirmed { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool IsUsingTemporaryPassword { get; set; } = false;
        public DateTime? TemporaryPasswordCreatedAt { get; set; }
        public int? OrganizationId { get; set; }
        public virtual Organization Organization { get; set; }
        public string? Justification { get; set; }
        public string? AcceptedEulaVersion { get; set; }
        public DateTime? EulaAcceptedAt { get; set; }
    }
}
