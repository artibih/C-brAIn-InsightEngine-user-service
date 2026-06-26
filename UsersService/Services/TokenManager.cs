using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using UsersService.Configuration;
using UsersService.DbContext;
using UsersService.Interfaces;
using UsersService.Models.Entities;
using UsersService.Models.Responses.Auth;

namespace UsersService.Services
{
    public class TokenManager : ITokenManager
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<TokenManager> _logger;
        private readonly RsaSecurityKey _rsaPrivateKey;
        private readonly JwtSettings _jwtSettings;
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _configuration;

        public TokenManager(
            UserManager<ApplicationUser> userManager,
            RsaSecurityKey rsaPrivateKey,
            JwtSettings jwtSettings,
            ApplicationDbContext db,
            IConfiguration configuration,
            ILogger<TokenManager> logger)
        {
            _userManager = userManager;
            _logger = logger;
            _rsaPrivateKey = rsaPrivateKey;
            _jwtSettings = jwtSettings;
            _configuration = configuration;
            _db = db;
        }

        public async Task<TokenResponse> GenerateTokensAsync(ApplicationUser user, CancellationToken ct)
        {
            var accessToken = await GenerateAccessToken(user);
            var refreshToken = await CreateOrUpdateRefreshTokenForUserAsync(user, DateTime.UtcNow, ct);

            return new TokenResponse
            {
                Token = accessToken,
                RefreshToken = refreshToken
            };
        }

        public async Task<TokenResponse?> RefreshTokensAsync(
            string refreshTokenPlaintext,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(refreshTokenPlaintext))
                return null;

            var tokenHash = ComputeSha256HashHex(refreshTokenPlaintext);
            var now = DateTime.UtcNow;

            var token = await _db.RefreshTokens
                .Include(r => r.User)
                .SingleOrDefaultAsync(r => r.TokenHash == tokenHash, ct);

            if (token == null || token.ExpiresAt <= now || token.User == null)
            {
                _logger.LogWarning("RefreshTokensAsync failed: Token not found, expired, or user missing.");
                return null;
            }

            if (!token.User.EmailConfirmed)
            {
                _logger.LogWarning("RefreshTokensAsync blocked: email not confirmed for user {UserId}.", token.User.Id);
                return null;
            }

            var newRefreshTokenPlain = GenerateSecureRandomTokenBase64(64);
            var newRefreshTokenHash = ComputeSha256HashHex(newRefreshTokenPlain);
            var expirationInMinutes = _jwtSettings.RefreshExpirationInMinutes > 0 ? _jwtSettings.RefreshExpirationInMinutes : 20160;
            var newExpiresAt = now.AddMinutes(expirationInMinutes);

            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            try
            {
                token.TokenHash = newRefreshTokenHash;
                token.CreatedAt = now;
                token.ExpiresAt = newExpiresAt;

                await _db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency failure detected for user {UserId}.", token.User.Id);
                await transaction.RollbackAsync(ct);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token rotation for user {UserId}.", token.User.Id);
                await transaction.RollbackAsync(ct);
                return null;
            }

            var accessToken = await GenerateAccessToken(token.User);

            _logger.LogInformation("Refresh token rotated successfully for user {UserId}.", token.User.Id);

            return new TokenResponse
            {
                Token = accessToken,
                RefreshToken = newRefreshTokenPlain,
            };
        }

        public async Task<string> GenerateAccessToken(ApplicationUser user)
        {

            var roles = await _userManager.GetRolesAsync(user);
            return await GenerateAccessToken(user, roles.ToList());

        }

        public async Task<string> GenerateAccessToken(ApplicationUser user, IReadOnlyList<string> roles)
        {
            try
            {

                var expirationMinutes = _jwtSettings.AccessExpirationInMinutes > 0
                    ? _jwtSettings.AccessExpirationInMinutes
                    : 15;

                var claims = new List<Claim>
                {
                    new (ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new (ClaimTypes.Name, user.UserName),
                    new (ClaimTypes.GroupSid, user.OrganizationId?.ToString() ?? "")
                };

                if (user.OrganizationId.HasValue)
                    claims.Add(new Claim("organization_id", user.OrganizationId.Value.ToString()));

                claims.Add(new Claim("org_scope_level", "organization"));


                claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

                if (!double.TryParse(_configuration["JwtSettings:AccessExpirationInMinutes"], out _))
                {
                    _logger.LogWarning("Invalid access token expiration setting. Using default of 60 minutes.");
                }
                foreach (var aud in _jwtSettings.Audiences.Where(a => !string.IsNullOrWhiteSpace(a)).Distinct())
                {
                    claims.Add(new Claim(JwtRegisteredClaimNames.Aud, aud));
                }

                var descriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(claims),
                    Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
                    SigningCredentials = new SigningCredentials(_rsaPrivateKey, SecurityAlgorithms.RsaSha256),
                    Issuer = _jwtSettings.Issuer,
                };

                var token = CreateToken(descriptor);

                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GenerateAccessToken: Unhandled exception (UserId={UserId})", user.Id);
                throw;
            }
        }

        private static string GenerateSecureRandomTokenBase64(int lengthBytes)
        {
            var bytes = new byte[lengthBytes];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }
        private static string ComputeSha256HashHex(string value)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(value);
            var hash = sha.ComputeHash(bytes);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private async Task<string> CreateOrUpdateRefreshTokenForUserAsync(ApplicationUser user, DateTime now, CancellationToken ct)
        {
            var existingToken = await _db.RefreshTokens
                .SingleOrDefaultAsync(r => r.UserId == user.Id, ct);

            var newTokenPlain = GenerateSecureRandomTokenBase64(64);
            var newTokenHash = ComputeSha256HashHex(newTokenPlain);

            var expirationInMinutes = _jwtSettings.RefreshExpirationInMinutes > 0 ? _jwtSettings.RefreshExpirationInMinutes : 20160;

            if (existingToken != null)
            {
                existingToken.TokenHash = newTokenHash;
                existingToken.CreatedAt = now;
                existingToken.ExpiresAt = now.AddMinutes(expirationInMinutes);
            }
            else
            {
                var entity = new RefreshToken
                {
                    UserId = user.Id,
                    TokenHash = newTokenHash,
                    CreatedAt = now,
                    ExpiresAt = now.AddMinutes(expirationInMinutes)
                };
                await _db.RefreshTokens.AddAsync(entity, ct);
            }

            await _db.SaveChangesAsync(ct);

            return newTokenPlain;
        }

        private string CreateToken(SecurityTokenDescriptor descriptor)
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.CreateToken(descriptor);
            return handler.WriteToken(token);
        }

        private int GetSecondsUntilExpiration(string? expClaimValue)
        {
            if (string.IsNullOrEmpty(expClaimValue)) return 0;
            if (!long.TryParse(expClaimValue, out var expUnix)) return 0;

            var expDate = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
            return Math.Max(0, (int)(expDate - DateTime.UtcNow).TotalSeconds);
        }

        public int GetAccessTokenRemainingLifetime(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return 0;

            try
            {
                var handler = new JwtSecurityTokenHandler();

                var jwt = handler.ReadJwtToken(token);
                var expClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp);

                return GetSecondsUntilExpiration(expClaim?.Value);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public async Task<int> GetRefreshTokenRemainingLifetimeByUserAsync(int userId, CancellationToken ct)
        {
            var now = DateTime.UtcNow;

            var tokenEntity = await _db.RefreshTokens
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .FirstOrDefaultAsync(ct);

            if (tokenEntity == null) return 0;

            var remaining = tokenEntity.ExpiresAt - now;
            return remaining > TimeSpan.Zero ? (int)remaining.TotalSeconds : 0;
        }

    }
}