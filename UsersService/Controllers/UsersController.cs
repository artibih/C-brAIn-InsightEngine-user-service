using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using UsersService.Interfaces;
using UsersService.Models.Entities;
using UsersService.Models.Requests.Auth;
using UsersService.Models.Requests.Reports;
using UsersService.Models.Requests.Users;
using UsersService.Models.Responses.Users;
using UsersService.Util;

namespace UsersService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class UsersController(
        UserManager<ApplicationUser> userManager,
        ILogger<UsersController> logger,
        IMapper mapper,
        IEmailBuilder emailBuilder,
        IEmailSender emailSender,
        IUserService userService) : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager = userManager;
        private readonly ILogger<UsersController> _logger = logger;
        private readonly IMapper _mapper = mapper;
        private readonly IEmailSender _emailSender = emailSender;
        private readonly IEmailBuilder _emailBuilder = emailBuilder;
        private readonly IUserService _userService = userService;

        [HttpGet("{id}")]
        [Authorize(Roles = "Admin, SuperAdmin, OrganizationAdmin")]
        public async Task<IActionResult> GetUserById(int id, CancellationToken ct)
        {
            try
            {
                var userResponse = await _userService.GetUserByIdAsync(id, ct);

                if (userResponse == null)
                    return NotFound(new { Message = "User not found." });

                return Ok(userResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching user {UserId}", id);
                return StatusCode(500, new { Message = "An unexpected error occurred." });
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin, SuperAdmin, OrganizationAdmin")]
        public async Task<IActionResult> GetAllUsers(
             [FromQuery] int page = 1,
             [FromQuery] int pageSize = 10,
             [FromQuery] string? search = null,
             CancellationToken ct = default)
        {
            try
            {
                var role = User.GetUserRole().ToString();
                var userId = int.Parse(User.GetUserId());
                var orgId = int.Parse(User.GetOrgId());

                var result = await _userService.GetPaginatedUsersAsync(role, userId, orgId, page, pageSize, search, ct);

                return Ok(new { Data = result });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { ex.Message });
            }
            catch
            {
                return StatusCode(500, new { Message = "An unexpected error occurred while processing your request." });
            }
        }

        [HttpGet("count")]
        public async Task<IActionResult> GetUsersCountAsync()
        {
            var count = await _userService.GetUsersCountAsync();
            return Ok(count);
        }

        [HttpPost]
        [Authorize(Roles = "Admin, SuperAdmin, OrganizationAdmin")]
        public async Task<IActionResult> AddUser([FromBody] CreateUserRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                    return Unauthorized();

                var newUser = await _userService.CreateUserAsync(request, currentUser, ct);

                var userResponse = _mapper.Map<UsersResponse>(newUser);
                return CreatedAtAction(nameof(GetUserById), new { id = newUser.Id }, userResponse);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Validation failed during user creation.");
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create a new user.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the user.");
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin, SuperAdmin, OrganizationAdmin")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _userService.UpdateUserAsync(id, request);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "User not found.");
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Validation failed during user update.");
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while updating user {UserId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while updating the user." });
            }
        }
        [HttpPut("{id}/admin")]
        [Authorize(Roles = "Admin, SuperAdmin, OrganizationAdmin")]
        public async Task<IActionResult> UpdateUserAdmin(int id, [FromBody] UpdateUserAdminRequest request)
        {          
            var hasAny =
                request.RoleId.HasValue ||
                !string.IsNullOrWhiteSpace(request.NewPassword);

            if (!hasAny)
                return BadRequest(new { message = "No changes provided." });

            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                    return Unauthorized(new { message = "Current user not found." });

                await _userService.UpdateUserAdminAsync(id, request, currentUser);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "User not found.");
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Validation failed during admin update.");
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while admin-updating user {UserId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while updating the user." });
            }
        }


        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin, SuperAdmin, OrganizationAdmin")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Unauthorized(new { message = "Current user not found." });
                }

                await _userService.DeleteUserAsync(id, currentUser);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "User not found for deletion.");
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to delete user {UserId}", id);
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Validation failed during user deletion.");
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while deleting user {UserId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while deleting the user." });
            }
        }

        [HttpPut("{id}/roles")]
        [Authorize(Roles = "Admin, SuperAdmin, OrganizationAdmin")]
        public async Task<IActionResult> UpdateRoles(int id, [FromBody] UpdateRolesRequest request)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                return NotFound();
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var result = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            result = await _userManager.AddToRolesAsync(user, request.Roles);
            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            return NoContent();
        }

        [HttpPost("change-password")]
        [Authorize(Roles = "Admin, SuperAdmin")]
        public async Task<IActionResult> ChangePassword([FromBody] AdminChangePasswordRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.EmailAddress);

            if (user == null)
            {
                return NotFound(new { Message = "User not found" });
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);

            if (result.Succeeded)
            {
                var email = _emailBuilder.BuildChangedPasswordNotificationEmail(user.Email, user.FirstName);
                await _emailSender.SendEmailAsync(email);

                return Ok(new { Message = "Password changed successfully" });
            }

            return BadRequest(result.Errors);
        }

        [HttpPost("{id}/confirm-email")]
        [Authorize(Roles = "Admin, SuperAdmin")]
        public async Task<IActionResult> ConfirmEmail(int id, CancellationToken ct)
        {
            try
            {
                var approved = await _userService.ApproveUserAsync(id, ct);

                if (!approved)
                    return Ok(new { Message = "User is already approved." });

                return Ok(new { Message = "User email confirmed successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "User not found for approval.");
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Validation failed during user approval.");
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while approving user {UserId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while approving the user." });
            }
        }

        [HttpPost("report-problem")]
        public async Task<IActionResult> ReportProblem([FromBody] ProblemReportRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdString, out var userId))
            {
                return Unauthorized("Invalid authentication token.");
            }

            try
            {
                var created = await _userService.CreateProblemReportAsync(userId, request);
                return Ok(created);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (ApplicationException ex)
            {
                return StatusCode(502, new { error = "Failed to create Jira ticket", details = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "An unexpected error occurred.", details = ex.Message });
            }
        }
    }
}
