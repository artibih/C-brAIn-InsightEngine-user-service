using System.Security.Claims;
using UsersService.Models.Requests.Users;
using UsersService.Models.Responses.Users;

namespace UsersService.Interfaces
{
    public interface IAccountService
    {
        Task<UserResponse?> GetLoggedInUserAsync(ClaimsPrincipal userClaims);
        Task<object?> GetUserEmailAsync(ClaimsPrincipal userClaims);
        Task<(UserResponse? user, string? error)> UpdateLoggedInUserAsync(ClaimsPrincipal userClaims, UpdateLoggedUserRequest request, CancellationToken ct);
    }
}