using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UsersService.Interfaces;
using UsersService.Models.Entities;

namespace UsersService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITokenManager _tokenManager;
        private readonly IUserRoleCache _userRoleCache;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            ITokenManager tokenManager,
            IUserRoleCache userRoleCache,
            ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _tokenManager = tokenManager;
            _userRoleCache = userRoleCache;
            _logger = logger;
        }

        [HttpGet("me")]
        public async Task<IActionResult> Me(CancellationToken ct)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Validation failed – missing user ID claim");
                    return AccessTokenUnauthorizedResponse();
                }

                var token = await HttpContext.GetTokenAsync("access_token");

                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Access token not found in authentication properties");
                    return AccessTokenUnauthorizedResponse();
                }

                var accessExpRemainingSec = _tokenManager.GetAccessTokenRemainingLifetime(token);

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Validation failed – user not found (UserId={UserId})", userId);
                    return AccessTokenUnauthorizedResponse();
                }

                var refreshExpRemainingSec = await _tokenManager.GetRefreshTokenRemainingLifetimeByUserAsync(user.Id, ct);
                var roles = await _userManager.GetRolesAsync(user);

                return Ok(new
                {
                    authenticated = true,
                    user = new
                    {
                        id = user.Id,
                        name = string.IsNullOrWhiteSpace(user.FirstName) && string.IsNullOrWhiteSpace(user.LastName)
                            ? user.UserName
                            : $"{user.FirstName} {user.LastName}".Trim(),
                        roles = roles,
                        acceptedEulaVersion = user.AcceptedEulaVersion
                    },
                    accessExpRemainingSec,
                    refreshExpRemainingSec = refreshExpRemainingSec
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception during auth check.");
                return StatusCode(500, new { Message = "An error occurred while processing your request." });
            }
        }

        [Authorize(AuthenticationSchemes = "Identity.Application")]
        [HttpGet("session")]
        public async Task<IActionResult> GetSession(CancellationToken ct)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Session validation failed – missing user ID claim");
                    return Unauthorized();
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Session validation failed – user not found (UserId={UserId})", userId);
                    return Unauthorized();
                }

                var roles = await _userRoleCache.GetRolesAsync(user, ct);

                var accessToken = await _tokenManager.GenerateAccessToken(user, roles);

                return Ok(new
                {
                    authenticated = true,
                    token = accessToken,
                    user = new
                    {
                        id = user.Id,
                        name = string.IsNullOrWhiteSpace(user.FirstName) && string.IsNullOrWhiteSpace(user.LastName)
                            ? user.UserName
                            : $"{user.FirstName} {user.LastName}".Trim(),
                        roles = roles
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception during session restoration.");
                return StatusCode(500, new { Message = "An error occurred while restoring the session." });
            }
        }



        private IActionResult AccessTokenUnauthorizedResponse()
        {
            return Unauthorized(new
            {
                code = "unauthorized",
                message = "Access token is invalid or expired"
            });
        }
    }
}