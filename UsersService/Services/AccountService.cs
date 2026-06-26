using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using UsersService.Interfaces;
using UsersService.Models.Entities;
using UsersService.Models.Requests.Users;
using UsersService.Models.Responses.Users;

namespace UsersService.Services
{
    public class AccountService : IAccountService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMapper _mapper;
        private readonly ILogger<UserService> _logger;

        public AccountService(UserManager<ApplicationUser> userManager, IMapper mapper, ILogger<UserService> logger)
        {
            _userManager = userManager;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<UserResponse?> GetLoggedInUserAsync(ClaimsPrincipal userClaims)
        {
            var userIdClaim = userClaims.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                _logger.LogWarning("No NameIdentifier claim found for current user.");
                return null;
            }

            if (!int.TryParse(userIdClaim.Value, out int userId))
            {
                _logger.LogWarning("Invalid user ID claim: {ClaimValue}", userIdClaim.Value);
                return null;
            }

            var user = await _userManager.Users
                .Include(u => u.Organization)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                _logger.LogWarning("User not found for ID: {UserId}", userId);
                return null;
            }

            return _mapper.Map<UserResponse>(user);
        }

        public async Task<object?> GetUserEmailAsync(ClaimsPrincipal userClaims)
        {
            var userIdClaim = userClaims.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                _logger.LogWarning("No NameIdentifier claim found for current user.");
                return null;
            }

            if (!int.TryParse(userIdClaim.Value, out int userId))
            {
                _logger.LogWarning("Invalid user ID claim: {ClaimValue}", userIdClaim.Value);
                return null;
            }

            var email = await _userManager.Users
                .Where(u => u.Id == userId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            if (email == null)
            {
                _logger.LogWarning("User not found for ID: {UserId}", userId);
                return null;
            }

            return new { Email = email };
        }

        public async Task<(UserResponse? user, string? error)> UpdateLoggedInUserAsync(
         ClaimsPrincipal userClaims,
         UpdateLoggedUserRequest request,
         CancellationToken ct)
        {
            var userIdClaim = userClaims.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return (null, "Invalid user identity.");

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user == null)
                return (null, "User not found.");

            var existing = await _userManager.FindByNameAsync(request.UserName);
            if (existing != null && existing.Id != userId)
                return (null, "Username already exists.");

            user.UserName = request.UserName;
            user.FirstName = request.FirstName;
            user.LastName = request.LastName;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var error = string.Join(", ", result.Errors.Select(e => e.Description));
                return (null, error);
            }

            return (_mapper.Map<UserResponse>(user), null);
        }
    }
}