using System.ComponentModel.DataAnnotations;

namespace UsersService.Models.Entities
{
    public class RefreshToken
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [ConcurrencyCheck]
        public string TokenHash { get; set; }

        public int UserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime ExpiresAt { get; set; }

        public ApplicationUser? User { get; set; }

    }
}