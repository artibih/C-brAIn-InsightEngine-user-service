using UsersService.Models.Entities;
using UsersService.Models.Responses.Auth;

namespace UsersService.Interfaces
{
    public interface ITokenManager
    {
        Task<TokenResponse> GenerateTokensAsync(ApplicationUser user, CancellationToken ct);
        Task<string> GenerateAccessToken(ApplicationUser user);
        Task<string> GenerateAccessToken(ApplicationUser user, IReadOnlyList<string> roles);
        int GetAccessTokenRemainingLifetime(string token);
        Task<int> GetRefreshTokenRemainingLifetimeByUserAsync(int userId, CancellationToken ct);
        Task<TokenResponse?> RefreshTokensAsync(string refreshToken, CancellationToken ct);
    }
}
