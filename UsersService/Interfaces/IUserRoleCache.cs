using UsersService.Models.Entities;

namespace UsersService.Interfaces
{
    public interface IUserRoleCache
    {
        Task<IReadOnlyList<string>> GetRolesAsync(ApplicationUser user, CancellationToken ct);
    }
}
