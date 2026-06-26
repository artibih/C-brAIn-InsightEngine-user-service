using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using UsersService.Configuration;
using UsersService.DbContext;
using UsersService.Interfaces;
using UsersService.Models.Entities;
using UsersService.Models.Helpers;
using UsersService.Models.Requests.Reports;
using UsersService.Models.Requests.Users;
using UsersService.Models.Responses.Reports;
using UsersService.Models.Responses.Users;

namespace UsersService.Services
{
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ApplicationDbContext _dbContext;
        private readonly IConfiguration _config;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMapper _mapper;
        private readonly IUtilityService _utilityService;
        private readonly IEmailSender _emailSender;
        private readonly IEmailBuilder _emailBuilder;
        private readonly ApplicationSettings _settings;
        private readonly ILogger<UserService> _logger;

        public UserService(
            ApplicationDbContext db,
            IHttpClientFactory httpClientFactory,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext dbContext,
            IMapper mapper,
            IUtilityService utilityService,
            IEmailSender emailSender,
            IEmailBuilder emailBuilder,
            IOptions<ApplicationSettings> settings,
            ILogger<UserService> logger,
            IConfiguration config)
        {
            _db = db;
            _httpClientFactory = httpClientFactory;
            _userManager = userManager;
            _mapper = mapper;
            _dbContext = dbContext;
            _config = config;
            _utilityService = utilityService;
            _emailSender = emailSender;
            _emailBuilder = emailBuilder;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<PagedResult<UsersResponse>> GetPaginatedUsersAsync(
             string? role,
             int userId,
             int orgId,
             int page,
             int pageSize,
             string? search = null,
             CancellationToken ct = default)
        {
            IQueryable<ApplicationUser> query = role switch
            {
                "Admin" or "SuperAdmin" => _userManager.Users,
                "OrganizationAdmin" =>
                    _userManager.Users.Where(u => u.OrganizationId == orgId),
                _ => throw new UnauthorizedAccessException("User does not have permission to view users.")
            };

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = $"%{search}%";
                query = query.Where(u =>
                     EF.Functions.ILike(u.FirstName + " " + u.LastName, $"%{term}%") ||
                     EF.Functions.ILike(u.Email, $"%{term}%"));
            }

            var totalItems = await query.CountAsync(ct);

    
            var users = await (
                from u in query
                join o in _dbContext.Organizations on u.OrganizationId equals o.Id into orgs
                from org in orgs.DefaultIfEmpty()
                orderby u.Id == userId descending, u.FirstName
                select new
                {
                    u.Id,
                    u.UserName,
                    u.FirstName,
                    u.LastName,
                    u.Email,
                    u.EmailConfirmed,
                    u.OrganizationId,
                    OrgName = org != null ? org.Name : null,
                    u.Justification
                }
            )
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

            var userIds = users.Select(x => x.Id).ToList();
            var userRolesMap = await _dbContext.UserRoles
                .Where(ur => userIds.Contains(ur.UserId))
                .Join(_dbContext.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
                .GroupBy(x => x.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.Select(x => x.Name).ToList(), ct);

            var usersWithRoles = users.Select(u => new UsersResponse
            {
                Id = u.Id,
                UserName = u.UserName,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = u.Email,
                EmailConfirmed = u.EmailConfirmed,
                OrganizationId = u.OrganizationId,
                OrganizationName = u.OrgName,
                Justification = u.Justification,
                Roles = userRolesMap.GetValueOrDefault(u.Id, [])
            }).ToList();

            return new PagedResult<UsersResponse>(usersWithRoles, totalItems, page, pageSize);
        }

        public async Task<UsersResponse?> GetUserByIdAsync(int id, CancellationToken ct = default)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
                return null;

            var roles = await _userManager.GetRolesAsync(user);

            var userResponse = _mapper.Map<UsersResponse>(user);
            userResponse.Roles = roles.ToList();

            if (user.OrganizationId.HasValue)
            {
                var org = await _dbContext.Organizations.FindAsync(user.OrganizationId.Value);
                userResponse.OrganizationName = org?.Name;
            }

            return userResponse;
        }

        public async Task<ApplicationUser> CreateUserAsync(CreateUserRequest request, ApplicationUser currentUser, CancellationToken ct)
        {
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
                throw new InvalidOperationException($"A user with the email '{request.Email}' already exists.");

            var currentUserRoles = await _userManager.GetRolesAsync(currentUser);
            int? organizationIdToAssign = null;

            if (currentUserRoles.Contains("OrganizationAdmin"))
            {
                organizationIdToAssign = currentUser.OrganizationId ?? throw new InvalidOperationException("Your user account is not associated with any organization.");
            }
            else
            {
                if (!request.OrganizationId.HasValue)
                    throw new InvalidOperationException("OrganizationId must be provided.");

                var orgExists = await _dbContext.Organizations.AnyAsync(o => o.Id == request.OrganizationId.Value, ct);
                if (!orgExists)
                    throw new InvalidOperationException($"Organization with ID {request.OrganizationId.Value} does not exist.");

                organizationIdToAssign = request.OrganizationId.Value;
            }

            var tempPassword = _utilityService.GenerateSecurePassword(12);

            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                EmailConfirmed = true,
                OrganizationId = organizationIdToAssign,
                IsUsingTemporaryPassword = false
            };

            var createResult = await _userManager.CreateAsync(user, tempPassword);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"User creation failed: {errors}");
            }

            var roleResult = await _userManager.AddToRoleAsync(user, "User");
            if (!roleResult.Succeeded)
            {
                var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to assign role: {errors}");
            }

            try
            {
                var emailToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);

                var email = _emailBuilder.BuildTemporaryPasswordEmail(
                    user.Email,
                    user.FirstName,
                    tempPassword,
                    _settings.FrontendBaseUrl
                );

                await _emailSender.SendEmailAsync(email);

                _logger.LogInformation("Temporary password and confirmation email sent to new user: {Email}", user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send confirmation email to {Email}", user.Email);
                throw new InvalidOperationException("Failed to send confirmation email.");
            }

            return user;
        }

        public async Task UpdateUserAsync(int id, UpdateUserRequest request)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
                throw new KeyNotFoundException($"User with ID {id} was not found.");

            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null && existingUser.Id != user.Id)
                throw new InvalidOperationException($"A user with the email '{request.Email}' already exists.");

            user.UserName = request.Email;
            user.Email = request.Email;
            user.FirstName = request.FirstName;
            user.LastName = request.LastName;
            user.OrganizationId = request.OrganizationId;

            _mapper.Map(request, user);

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"User update failed: {errors}");
            }
        }

        public async Task<bool> ApproveUserAsync(int id, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
                throw new KeyNotFoundException($"User with ID {id} was not found.");

            if (user.EmailConfirmed)
                return false;

            user.EmailConfirmed = true;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"User approval failed: {errors}");
            }

            try
            {
                var loginUrl = GetFrontendLoginUrl();
                var userName = GetUserDisplayName(user);
                var email = _emailBuilder.BuildAccountApprovedEmail(user.Email, userName, loginUrl);

                await _emailSender.SendEmailAsync(email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send approval email to user {UserId} at {Email}", user.Id, user.Email);
            }

            return true;
        }

        private string GetFrontendLoginUrl()
        {
            var loginUrl = _settings.FrontendBaseUrl;
            if (string.IsNullOrWhiteSpace(loginUrl))
                loginUrl = _config["ApplicationSettings:FrontendBaseUrl"];

            if (string.IsNullOrWhiteSpace(loginUrl))
                throw new InvalidOperationException("Frontend base URL is not configured.");

            return loginUrl;
        }

        private static string GetUserDisplayName(ApplicationUser user)
        {
            var fullName = $"{user.FirstName} {user.LastName}".Trim();

            return string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName;
        }

        public async Task UpdateUserAdminAsync(int id, UpdateUserAdminRequest request, ApplicationUser currentUser)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
                throw new KeyNotFoundException($"User with ID {id} was not found.");

            var currentUserRoles = await _userManager.GetRolesAsync(currentUser);
            if (currentUserRoles.Contains("OrganizationAdmin"))
            {
                if (currentUser.OrganizationId == null || currentUser.OrganizationId != user.OrganizationId)
                    throw new UnauthorizedAccessException("You are not allowed to update users from other organizations.");
            }

            if (request.RoleId.HasValue)
            {
                var roleName = request.RoleId.Value switch
                {
                    1 => "Admin",
                    2 => "User",
                    3 => "CBrainUser",
                    _ => throw new InvalidOperationException("Invalid roleId. Allowed: 1 (Admin), 2 (User), 3 (CBrainUser).")
                };

                var currentRolesOfUser = await _userManager.GetRolesAsync(user);

                var alreadyHasOnlyThat =
                    currentRolesOfUser.Count == 1 && string.Equals(currentRolesOfUser[0], roleName, StringComparison.OrdinalIgnoreCase);

                if (!alreadyHasOnlyThat)
                {
                    var remove = await _userManager.RemoveFromRolesAsync(user, currentRolesOfUser);
                    if (!remove.Succeeded)
                        throw new InvalidOperationException("Failed to remove existing roles: " +
                            string.Join(", ", remove.Errors.Select(e => e.Description)));

                    var add = await _userManager.AddToRoleAsync(user, roleName);
                    if (!add.Succeeded)
                        throw new InvalidOperationException("Failed to assign role: " +
                            string.Join(", ", add.Errors.Select(e => e.Description)));
                }
            }

            if (!string.IsNullOrWhiteSpace(request.NewPassword))
            {
                var newPwd = request.NewPassword.Trim();

                if (newPwd.Length < 8)
                    throw new InvalidOperationException("Password must be at least 8 characters.");

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var pwRes = await _userManager.ResetPasswordAsync(user, token, newPwd);
                if (!pwRes.Succeeded)
                    throw new InvalidOperationException("Password reset failed: " +
                        string.Join(", ", pwRes.Errors.Select(e => e.Description)));

                user.IsUsingTemporaryPassword = false;

                var upd = await _userManager.UpdateAsync(user);
                if (!upd.Succeeded)
                    throw new InvalidOperationException("Failed to update user after password change: " +
                        string.Join(", ", upd.Errors.Select(e => e.Description)));
            }
        }


        public async Task DeleteUserAsync(int id, ApplicationUser currentUser)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
                throw new KeyNotFoundException($"User with ID {id} was not found.");

            var currentUserRoles = await _userManager.GetRolesAsync(currentUser);

            if (currentUserRoles.Contains("OrganizationAdmin"))
            {
                if (currentUser.OrganizationId == null || currentUser.OrganizationId != user.OrganizationId)
                {
                    throw new UnauthorizedAccessException("You are not allowed to delete users from other organizations.");
                }
            }

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to delete user: {errors}");
            }
        }

        public async Task<ProblemReportResponse> CreateProblemReportAsync(int reporterId, ProblemReportRequest request)
        {
            var user = await _db.Users
                                .AsNoTracking()
                                .FirstOrDefaultAsync(u => u.Id == reporterId)
                       ?? throw new InvalidOperationException("Reporter not found");

            var jiraKey = await TryCreateJiraIssueAsync(request);

            return new ProblemReportResponse
            {
                ReporterId = reporterId,
                OrganizationId = user.OrganizationId ?? 0,
                Title = request.Title,
                Category = request.Category,
                Description = request.Description,
                StepsToReproduce = request.StepsToReproduce,
                OccurredAt = request.OccurredAt ?? DateTime.UtcNow,
                JiraIssueKey = jiraKey
            };
        }

        public async Task<int> GetUsersCountAsync()
        {
            return await _db.Users.CountAsync();
        }

        private async Task<string> TryCreateJiraIssueAsync(ProblemReportRequest request)
        {
            var baseUrl = _config["Jira:BaseUrl"];
            var username = _config["Jira:Username"];
            var apiToken = _config["Jira:ApiToken"];
            var project = _config["Jira:ProjectKey"];

            if (string.IsNullOrWhiteSpace(baseUrl) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(apiToken) ||
                string.IsNullOrWhiteSpace(project))
            {
                throw new InvalidOperationException("Jira is not properly configured.");
            }

            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{apiToken}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);

            var formattedTitle = $"[{request.Category}] {request.Title}";
            var occurredAtText = request.OccurredAt.HasValue
                                ? $"\n\nOccurred At: {request.OccurredAt.Value.ToString("yyyy-MM-dd HH:mm:ss")}"
                                : string.Empty;

            var payload = new
            {
                fields = new
                {
                    project = new { key = project },
                    summary = formattedTitle,
                    description = $"{request.Description}\n\nSteps to reproduce:\n{request.StepsToReproduce}{occurredAtText}",
                    issuetype = new { name = "Bug" },
                    labels = new[] { request.Category.ToString() }
                }
            };

            var response = await client.PostAsJsonAsync("rest/api/2/issue", payload);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new ApplicationException($"Failed to create Jira issue. Status: {(int)response.StatusCode} {response.ReasonPhrase}. Details: {errorContent}");
            }

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            return doc.RootElement.GetProperty("key").GetString();
        }

    }
}
