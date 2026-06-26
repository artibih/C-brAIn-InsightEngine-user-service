using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using UsersService.Configuration;
using UsersService.DbContext;
using UsersService.Interfaces;
using UsersService.Models.Entities;
using UsersService.Models.Requests.Auth;
using UsersService.Models.Requests.Users;
using UsersService.Util;

namespace UsersService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AccountsController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IEmailSender emailSender, IEmailBuilder emailBuilder, ITokenManager tokenManager, IOptions<ApplicationSettings> settings, ILogger<AccountsController> logger, ApplicationDbContext context, IConfiguration configuration, IAccountService accountService) : ControllerBase
    {
        private readonly ApplicationSettings _settings = settings.Value;
        private readonly UserManager<ApplicationUser> _userManager = userManager;
        private readonly SignInManager<ApplicationUser> _signInManager = signInManager;
        private readonly ITokenManager _tokenManager = tokenManager;
        private readonly IEmailSender _emailSender = emailSender;
        private readonly IEmailBuilder _emailBuilder = emailBuilder;
        private readonly ILogger<AccountsController> _logger = logger;
        private readonly ApplicationDbContext _context = context;
        private readonly IConfiguration _configuration = configuration;
        private readonly IAccountService _accountService = accountService;

       
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterUser([FromBody] RegistrationRequest requestBody)
        {
            _logger.LogInformation("RegisterUser called for email: {Email}", requestBody.Email);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(new { message = "Invalid input.", errors });
            }

            var roleName = requestBody.RoleId switch
            {
                "1" => "Admin",
                "2" => "User",
                "3" => "CBrainUser",
                _ => null
            };

            int orgId;
            bool createdNewOrg = false;

            if (requestBody.OrganizationId.HasValue)
            {
                var existingOrg = await _context.Organizations.FindAsync(requestBody.OrganizationId.Value);
                if (existingOrg == null)
                    return BadRequest(new { message = "Organization does not exist." });
                orgId = existingOrg.Id;
            }
            else if (!string.IsNullOrWhiteSpace(requestBody.OrganizationName))
            {
                createdNewOrg = true;
                orgId = 0; 
            }
            else
            {
                return BadRequest(new { message = "Either an existing organization or a new organization name must be provided." });
            }

            var existingUser = await _userManager.FindByEmailAsync(requestBody.Email);
            if (existingUser != null)
                return BadRequest(new { message = "A user with this email already exists." });

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (createdNewOrg)
                {
                    var newOrg = new Organization
                    {
                        Name = requestBody.OrganizationName!.Trim(),
                        Email = requestBody.Email,
                        Description = "",
                        Address = "",
                        City = "",
                        Country = "",
                        Phone = "",
                        Website = "",
                        Logo = ""
                    };
                    _context.Organizations.Add(newOrg);
                    await _context.SaveChangesAsync();
                    orgId = newOrg.Id;
                }

                var isAdminCreating = User.Identity?.IsAuthenticated == true
                    && (User.IsInRole("Admin") || User.IsInRole("SuperAdmin") || User.IsInRole("OrganizationAdmin"));

                var newUser = new ApplicationUser
                {
                    UserName = requestBody.Email,
                    Email = requestBody.Email,
                    FirstName = requestBody.FirstName,
                    LastName = requestBody.LastName,
                    OrganizationId = orgId,
                    EmailConfirmed = isAdminCreating,
                    IsUsingTemporaryPassword = false,
                    Justification = requestBody.Justification,
                    AcceptedEulaVersion = string.IsNullOrWhiteSpace(requestBody.AcceptedEulaVersion)
                        ? null
                        : requestBody.AcceptedEulaVersion.Trim(),
                    EulaAcceptedAt = string.IsNullOrWhiteSpace(requestBody.AcceptedEulaVersion)
                        ? null
                        : DateTime.UtcNow
                };

                var createResult = await _userManager.CreateAsync(newUser, requestBody.Password);
                if (!createResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    var errors = createResult.Errors.Select(e => e.Description);
                    _logger.LogError("Failed to create user: {Errors}", string.Join(", ", errors));
                    return BadRequest(new { message = "User registration failed.", errors });
                }

                var roleResult = await _userManager.AddToRoleAsync(newUser, roleName);
                if (!roleResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    var errors = roleResult.Errors.Select(e => e.Description);
                    _logger.LogError("Failed to assign role: {Errors}", string.Join(", ", errors));
                    return BadRequest(new { message = "Failed to assign role.", errors });
                }

                await transaction.CommitAsync();

                return Ok(new
                {
                    message = "User registered successfully.",
                    userId = newUser.Id,
                    role = roleName,
                    organizationId = orgId
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Registration failed for {Email}, transaction rolled back.", requestBody.Email);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Registration failed." });
            }
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
        {
            _logger.LogInformation("Login attempt for email: {Email}", request.EmailAddress);

            var user = await _userManager.FindByEmailAsync(request.EmailAddress);

            if (user == null)
            {
                _logger.LogWarning("Login failed: User not found for email {Email}", request.EmailAddress);
                return NotFound(new { Message = "User not found" });
            }

            if (!user.EmailConfirmed)
            {
                _logger.LogWarning("Login blocked: email not confirmed for user {UserId}", user.Id);
                return StatusCode(StatusCodes.Status403Forbidden, new { Message = "Your account is pending approval. Please wait for an admin to approve your access request." });
            }

            var signInResult = await _signInManager.PasswordSignInAsync(user.UserName, request.Password, isPersistent: request.IsPersistent, lockoutOnFailure: false);

            if (signInResult.Succeeded)
            {
                _logger.LogInformation("Login successful for newUser {UserId}", user.Id);

                var tokens = await _tokenManager.GenerateTokensAsync(user, ct);

                return Ok(new { tokens.Token, tokens.RefreshToken, Message = "Login successful" });
            }
            else if (signInResult.IsLockedOut)
            {
                _logger.LogWarning("Login failed: Account locked out for newUser {UserId}", user.Id);
                return StatusCode(StatusCodes.Status403Forbidden, new { Message = "Account locked out" });
            }

            _logger.LogWarning("Invalid login attempt for newUser {UserId}", user.Id);
            return BadRequest(new { Message = "Invalid login attempt" });
        }

        [Authorize(AuthenticationSchemes = "Identity.Application")]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                await _signInManager.SignOutAsync();

                _logger.LogInformation("User logged out successfully.");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during logout.");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost("auto-login")]
        public async Task<IActionResult> ExternalAutoLogin([FromQuery] string token, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest(new { message = "No token provided in the request." });
            }

            var principal = ValidateExternalToken(token);

            if (principal == null)
            {
                return Unauthorized(new { message = "Invalid or expired external token." });
            }

            _logger.LogInformation("Listing all claims from principal:");
            foreach (var claim in principal.Claims)
            {
                _logger.LogInformation("Claim Type: {type}, Claim Value: {value}", claim.Type, claim.Value);
            }

            var externalEmail = principal.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value ?? principal.FindFirst("email")?.Value;
            _logger.LogInformation("Email: {emailAddress}", externalEmail);

            if (string.IsNullOrEmpty(externalEmail))
            {
                return BadRequest(new { message = "Required newUser information was not found in the token claims." });
            }

            var user = await _userManager.FindByEmailAsync(externalEmail);
            if (user == null)
            {
                return BadRequest(new { message = "The newUser does not exist in C-brAIn." });
            }

            if (!user.EmailConfirmed)
            {
                _logger.LogWarning("Auto-login blocked: email not confirmed for user {UserId}", user.Id);
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Your account is pending approval. Please wait for an admin to approve your access request." });
            }

            await _signInManager.SignInAsync(user, isPersistent: false);

            var tokens = await _tokenManager.GenerateTokensAsync(user, ct);

            var authData = new
            {
                authToken = tokens.Token,
                refreshToken = tokens.RefreshToken
            };
            var authDataJson = JsonConvert.SerializeObject(authData);
            var encodedAuthData = WebUtility.UrlEncode(authDataJson);
            var frontendBaseUrl = _configuration["ApplicationSettings:FrontendBaseUrl"];
            var redirectUrl = $"{frontendBaseUrl}auth/redirect?authData={encodedAuthData}";
            return Redirect(redirectUrl);
        }


        private ClaimsPrincipal? ValidateExternalToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            var secretKey = Environment.GetEnvironmentVariable("ExternalAuthSecretKey") ?? _configuration["ExternalAuth:SecretKey"];
            var validIssuers = _configuration.GetSection("ExternalAuth:ValidIssuers").Get<string[]>();
            var validAudiences = _configuration.GetSection("ExternalAuth:ValidAudiences").Get<string[]>();
            var clockSkewSeconds = int.TryParse(_configuration["ExternalAuth:ClockSkewSeconds"], out int seconds)
                ? seconds
                : 0;

            if (string.IsNullOrEmpty(secretKey) || validIssuers == null || validIssuers.Length == 0 || validAudiences == null || validAudiences.Length == 0)
            {
                _logger.LogError("External token configuration is not properly set.");
                return null;
            }

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuers = validIssuers,
                ValidateAudience = true,
                ValidAudiences = validAudiences,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,
                ClockSkew = TimeSpan.FromSeconds(clockSkewSeconds)
            };

            try
            {
                var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
                return principal;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "External token validation failed.");
                return null;
            }
        }

        [HttpGet("confirm-email", Name = "confirm-email")]
        public async Task<IActionResult> ConfirmEmail([FromQuery] string token, [FromQuery] string email)
        {
            _logger.LogInformation("ConfirmEmail called for email: {Email}", email);

            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(email))
            {
                _logger.LogWarning("Invalid token or email provided to ConfirmEmail.");
                return BadRequest(new { message = "Invalid token or email." });
            }

            string decodedToken;
            try
            {
                byte[] tokenBytes = WebEncoders.Base64UrlDecode(token);
                decodedToken = Encoding.UTF8.GetString(tokenBytes);
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, "Invalid token format for email: {Email}", email);
                return BadRequest(new { message = "Invalid token format." });
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                _logger.LogWarning("User not found for email: {Email}", email);
                return NotFound(new { message = "User not found." });
            }

            if (user.EmailConfirmed)
            {
                _logger.LogInformation("Email already confirmed for newUser: {UserId}", user.Id);
                return Ok(new { message = "Email is already confirmed." });
            }

            var result = await _userManager.ConfirmEmailAsync(user, decodedToken);
            if (result.Succeeded)
            {
                _logger.LogInformation("Email confirmed successfully for newUser: {UserId}", user.Id);
                return Redirect($"{_settings.FrontendBaseUrl}/confirmation-success");
            }

            _logger.LogError("Email confirmation failed for newUser: {UserId}. Errors: {Errors}",
                user.Id, string.Join(", ", result.Errors.Select(e => e.Description)));

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Email confirmation failed.",
                errors = result.Errors.Select(e => e.Description)
            });
        }


        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            _logger.LogInformation("ForgotPassword called for email: {Email}", request.EmailAddress);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for ForgotPassword: {Errors}", ModelState);
                return BadRequest(ModelState);
            }

            var user = await _userManager.FindByEmailAsync(request.EmailAddress);
            if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            {
                _logger.LogWarning("ForgotPassword failed: Email not verified or newUser not found for {Email}", request.EmailAddress);
                return BadRequest("Please verify your email first.");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var callbackUrl = $"{_settings.FrontendBaseUrl}/reset-password?userId={user.Id}&token={encodedToken}";

            var email = _emailBuilder.BuildPasswordResetEmail(user.Email, user.FirstName, callbackUrl);
            try
            {
                await _emailSender.SendEmailAsync(email);
                _logger.LogInformation("Password reset email sent to {Email}", user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset email to {Email}", user.Email);
                return StatusCode(500, "Failed to send password reset email.");
            }

            return Ok("Reset password link has been sent to your email.");
        }

        [HttpPost("reset-password", Name = "reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            _logger.LogInformation("ResetPassword called for newUser: {UserId}", request.UserId);

            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
            {
                _logger.LogWarning("ResetPassword failed: User not found for ID {UserId}", request.UserId);
                return BadRequest("Invalid request");
            }

            var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
            if (result.Succeeded)
            {
                _logger.LogInformation("Password reset successfully for newUser: {UserId}", user.Id);
                return Ok("Password has been reset successfully.");
            }

            _logger.LogError("Failed to reset password for newUser: {UserId}, Errors: {Errors}", user.Id, result.Errors);
            return BadRequest(result.Errors);
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            _logger.LogInformation("ChangePassword called for email: {Email}", request.Email);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for ChangePassword: {Errors}", ModelState);
                return BadRequest(ModelState);
            }

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                _logger.LogWarning("ChangePassword failed: User not found for email {Email}", request.Email);
                return BadRequest("User not found.");
            }

            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (result.Succeeded)
            {
                user.IsUsingTemporaryPassword = false;
                await _userManager.UpdateAsync(user);
                await _signInManager.RefreshSignInAsync(user);
                _logger.LogInformation("Password changed successfully for newUser: {UserId}", user.Id);
                return Ok("Password has been changed successfully.");
            }

            foreach (var error in result.Errors)
            {
                _logger.LogError("ChangePassword failed for newUser: {UserId}, Error: {Error}", user.Id, error.Description);
            }

            return BadRequest(result.Errors);
        }


        [HttpPost("resend-confirmation-email")]
        public async Task<IActionResult> ResendConfirmationEmail([FromBody] ResendEmailRequest request)
        {
            _logger.LogInformation("ResendConfirmationEmail called for email: {Email}", request.EmailAddress);

            var user = await _userManager.FindByEmailAsync(request.EmailAddress);
            if (user == null || await _userManager.IsEmailConfirmedAsync(user))
            {
                _logger.LogWarning("ResendConfirmationEmail failed: Invalid request for email {Email}", request.EmailAddress);
                return BadRequest("Invalid request.");
            }

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var confirmationLink = $"{_settings.FrontendBaseUrl}/confirm-email?token={token}&email={WebUtility.UrlEncode(user.Email)}";

            var email = _emailBuilder.BuildConfirmationEmail(user.Email, user.FirstName, confirmationLink);

            try
            {
                await _emailSender.SendEmailAsync(email);
                _logger.LogInformation("Confirmation email resent successfully to {Email}", user.Email);
                return Ok("Confirmation email resent successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resend confirmation email to {Email}", user.Email);
                return StatusCode(500, "Failed to resend the confirmation email. Please try again later.");
            }
        }

        [Authorize]
        [HttpGet("email")]
        public async Task<IActionResult> GetUserEmail()
        {
            var result = await _accountService.GetUserEmailAsync(User);
            if (result == null)
                return NotFound();

            return Ok(result);
        }

        [HttpGet("User")]
        [Authorize]
        public async Task<IActionResult> GetLoggedInUser()
        {
            try
            {
                var userDto = await _accountService.GetLoggedInUserAsync(User);

                if (userDto == null)
                    return Unauthorized(new { message = "User not authenticated or not found." });

                return Ok(userDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error fetching logged-in user.");
                return StatusCode(500, new { message = "Internal server error." });
            }
        }

        [Authorize]
        [HttpPatch("Me")]
        public async Task<IActionResult> UpdateLoggedInUser([FromBody] UpdateLoggedUserRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var (user, error) = await _accountService.UpdateLoggedInUserAsync(User, request, ct);

            if (user == null)
                return BadRequest(new { message = error });

            return Ok(user);
        }

        [Authorize]
        [HttpPost("accept-eula")]
        public async Task<IActionResult> AcceptEula([FromBody] AcceptEulaRequest request)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Version))
                return BadRequest(new { message = "EULA version is required." });

            var acks = request.Acknowledgments;
            if (acks is null || !acks.Agreement || !acks.PeerReview || !acks.DataUse || !acks.Liability)
                return BadRequest(new { message = "All EULA acknowledgments must be accepted." });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "User not authenticated." });

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return Unauthorized(new { message = "User not found." });

            user.AcceptedEulaVersion = request.Version.Trim();
            user.EulaAcceptedAt = DateTime.UtcNow;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                _logger.LogError("Failed to record EULA acceptance for user {UserId}: {Errors}",
                    user.Id, string.Join(", ", result.Errors.Select(e => e.Description)));
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to record EULA acceptance." });
            }

            _logger.LogInformation("EULA {Version} accepted by user {UserId}", user.AcceptedEulaVersion, user.Id);
            return Ok(new { acceptedEulaVersion = user.AcceptedEulaVersion, eulaAcceptedAt = user.EulaAcceptedAt });
        }

        [HttpPost("confirm-and-login")]
        public async Task<IActionResult> ConfirmAndLogin([FromBody] ConfirmAndLoginRequest request, CancellationToken ct)
        {
            _logger.LogInformation("ConfirmAndLogin attempt for email: {Email}", request.Email);

            if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Email))
            {
                _logger.LogWarning("Invalid token or email provided to ConfirmAndLogin.");
                return BadRequest(new { Message = "Invalid token or email." });
            }

            byte[] tokenBytes;
            try
            {
                tokenBytes = WebEncoders.Base64UrlDecode(request.Token);
            }
            catch (FormatException)
            {
                _logger.LogError("Invalid token format for email: {Email}", request.Email);
                return BadRequest(new { Message = "Invalid token format." });
            }

            string decodedToken = Encoding.UTF8.GetString(tokenBytes);

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                _logger.LogWarning("ConfirmAndLogin failed: User not found for email {Email}", request.Email);
                return NotFound(new { Message = "User not found" });
            }

            if (!user.EmailConfirmed)
            {
                var confirmResult = await _userManager.ConfirmEmailAsync(user, decodedToken);
                if (!confirmResult.Succeeded)
                {
                    _logger.LogError("Email confirmation failed for newUser: {UserId}", user.Id);
                    return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "Email confirmation failed." });
                }

                _logger.LogInformation("Email confirmed successfully for newUser: {UserId}", user.Id);
            }

            var signInResult = await _signInManager.PasswordSignInAsync(user.UserName, request.Password, isPersistent: false, lockoutOnFailure: false);

            if (signInResult.Succeeded)
            {
                _logger.LogInformation("Login successful for newUser {UserId}", user.Id);

                var tokens = await _tokenManager.GenerateTokensAsync(user, ct);

                return Ok(new { tokens.Token, tokens.RefreshToken, Message = "Email confirmed and login successful" });
            }
            else if (signInResult.IsLockedOut)
            {
                _logger.LogWarning("Login failed: Account locked out for newUser {UserId}", user.Id);
                return StatusCode(StatusCodes.Status403Forbidden, new { Message = "Account locked out" });
            }

            _logger.LogWarning("Invalid login attempt for newUser {UserId}", user.Id);
            return BadRequest(new { Message = "Invalid login attempt" });
        }


        [HttpPost("admin/create-newUser")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateUserWithTemporaryPassword([FromBody] AdminCreateUserRequest request)
        {
            _logger.LogInformation("CreateUserWithTemporaryPassword called by admin for email: {Email}", request.Email);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for CreateUserWithTemporaryPassword: {Errors}", ModelState);
                return BadRequest(ModelState);
            }

            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
            {
                _logger.LogWarning("CreateUserWithTemporaryPassword failed: User already exists for email {Email}", request.Email);
                return BadRequest("A newUser with this email already exists.");
            }

            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                EmailConfirmed = true,
                IsUsingTemporaryPassword = false,
                TemporaryPasswordCreatedAt = DateTime.UtcNow
            };

            var temporaryPassword = GenerateTemporaryPassword();
            var result = await _userManager.CreateAsync(user, temporaryPassword);

            if (result.Succeeded)
            {
                _logger.LogInformation("User created successfully by admin: {UserId}", user.Id);

                var roleResult = await _userManager.AddToRoleAsync(user, "User");
                if (!roleResult.Succeeded)
                {
                    _logger.LogError("Failed to assign User role to newUser: {UserId}, Errors: {Errors}", user.Id, roleResult.Errors);
                    return BadRequest(roleResult.Errors);
                }

                try
                {
                    var emailConfirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);

                    string loginAndConfirmUrl = $"{_settings.FrontendBaseUrl}/login-and-confirm?email={Uri.EscapeDataString(user.Email)}&token={Uri.EscapeDataString(emailConfirmationToken)}";

                    var emailContent = _emailBuilder.BuildTemporaryPasswordEmail(
                        user.Email,
                        user.FirstName,
                        temporaryPassword,
                        loginAndConfirmUrl
                    );

                    await _emailSender.SendEmailAsync(emailContent);
                    _logger.LogInformation("Temporary password and confirmation email sent to newUser: {Email}", user.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send temporary password and confirmation email to newUser: {Email}", user.Email);
                    return StatusCode(500, "Failed to send temporary password email.");
                }

                return Ok(new { UserId = user.Id, TemporaryPassword = temporaryPassword });
            }

            _logger.LogError("Failed to create newUser by admin: {Errors}", result.Errors);
            return BadRequest(result.Errors);
        }


        private static string GenerateTemporaryPassword()
        {
            const int passwordLength = 25;

            const string uppercaseLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lowercaseLetters = "abcdefghijklmnopqrstuvwxyz";
            const string numbers = "0123456789";
            const string specialCharacters = "!@#$%^&*()_-+=<>?{}[]|~`";

            var upperCaseArray = uppercaseLetters.ToCharArray();
            var lowerCaseArray = lowercaseLetters.ToCharArray();
            var numberArray = numbers.ToCharArray();
            var specialCharArray = specialCharacters.ToCharArray();

            var passwordChars = new char[passwordLength];

            using (var rng = RandomNumberGenerator.Create())
            {
                passwordChars[0] = upperCaseArray[GetRandomNumber(rng, upperCaseArray.Length)];

                passwordChars[1] = numberArray[GetRandomNumber(rng, numberArray.Length)];

                passwordChars[2] = specialCharArray[GetRandomNumber(rng, specialCharArray.Length)];

                var allChars = upperCaseArray.Concat(lowerCaseArray).Concat(numberArray).Concat(specialCharArray).ToArray();
                for (int i = 3; i < passwordLength; i++)
                {
                    passwordChars[i] = allChars[GetRandomNumber(rng, allChars.Length)];
                }

                ShuffleArray(passwordChars, rng);
            }

            return new string(passwordChars);
        }

        private static int GetRandomNumber(RandomNumberGenerator rng, int maxExclusive)
        {
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be greater than zero.");

            byte[] uint32Buffer = new byte[4];
            uint num;
            do
            {
                rng.GetBytes(uint32Buffer);
                num = BitConverter.ToUInt32(uint32Buffer, 0);
            } while (num >= uint.MaxValue - ((uint.MaxValue % (uint)maxExclusive) + 1) % (uint)maxExclusive);

            return (int)(num % maxExclusive);
        }

        private static void ShuffleArray(char[] array, RandomNumberGenerator rng)
        {
            int n = array.Length;
            while (n > 1)
            {
                n--;
                int k = GetRandomNumber(rng, n + 1);
                var temp = array[k];
                array[k] = array[n];
                array[n] = temp;
            }
        }
    }
}