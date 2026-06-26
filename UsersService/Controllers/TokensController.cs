using Microsoft.AspNetCore.Mvc;
using UsersService.Interfaces;
using UsersService.Models.Requests.Auth;

namespace UsersService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TokensController(
        ITokenManager tokenManager,
        ILogger<TokensController> logger) : ControllerBase
    {
        private readonly ITokenManager _tokenManager = tokenManager;
        private readonly ILogger<TokensController> _logger = logger;

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] TokenRefreshRequest request, CancellationToken ct)
        {
            try
            {
                var result = await _tokenManager.RefreshTokensAsync(request?.RefreshToken ?? "", ct);
                if (result == null)
                    return RefreshTokenUnauthorizedResponse();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception during refresh token.");
                return StatusCode(500, new { Message = "An error occurred while processing your request." });
            }
        }

        private IActionResult RefreshTokenUnauthorizedResponse()
        {
            return Unauthorized(new
            {
                code = "unauthorized",
                message = "Refresh token is invalid or expired"
            });
        }
    }
}