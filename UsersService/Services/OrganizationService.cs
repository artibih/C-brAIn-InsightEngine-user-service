using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UsersService.Configuration;
using UsersService.DbContext;
using UsersService.Enums;
using UsersService.Interfaces;
using UsersService.Models.Entities;
using UsersService.Models.Entities.Projections;
using UsersService.Models.Helpers;
using UsersService.Models.Requests.Organizations;
using UsersService.Models.Responses.Users;

namespace UsersService.Services
{
    public class OrganizationService : IOrganizationService
    {
        private readonly ILogger<OrganizationService> _logger;
        private readonly ApplicationDbContext _dbContext;
        private readonly IEmailSender _emailSender;
        private readonly IConfiguration _configuration;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailBuilder _emailBuilder;
        private readonly IUtilityService _utilityService;
        private readonly ApplicationSettings _settings;

        public OrganizationService(
            ILogger<OrganizationService> logger,
            ApplicationDbContext dbContext,
            IEmailSender emailSender,
            IUtilityService utilityService,
            IConfiguration configuration,
            IOptions<ApplicationSettings> settings,
            UserManager<ApplicationUser> userManager,
            IEmailBuilder emailBuilder
        )
        {
            _logger = logger;
            _dbContext = dbContext;
            _emailSender = emailSender;
            _configuration = configuration;
            _utilityService = utilityService;
            _userManager = userManager;
            _emailBuilder = emailBuilder;
            _settings = settings.Value;
        }

        private async Task<Organization> CreateOrganizationAsync(CreateOrganizationRequest organization)
        {
            _logger.LogInformation("Creating a new organization entry in the database.");

            var name = organization.Name.Trim();
            var exists = await _dbContext.Organizations
                            .AnyAsync(o => EF.Functions.ILike(o.Name, name));

            if (exists)
            {
                throw new InvalidOperationException($"An organization with the name '{organization.Name}' already exists.");
            }

            var newOrganization = new Organization
            {
                Name = organization.Name,
                Description = organization.Description,
                Country = organization.Country,
                City = organization.City,
                Address = organization.Address,
                Phone = organization.Phone,
                Email = organization.Email,
                Website = organization.Website,
                Logo = organization.Logo,
                ShouldSendEmail = organization.ShouldSendEmail,
            };

            _dbContext.Organizations.Add(newOrganization);
            await _dbContext.SaveChangesAsync();

            return newOrganization;
        }

        public async Task<Organization> CreateOrganizationWithAdminAsync(CreateOrganizationRequest request)
        {
            var organization = await CreateOrganizationAsync(request);

            var tempPassword = _utilityService.GenerateSecurePassword(12);

            var adminUser = new ApplicationUser
            {
                UserName = request.AdminEmail,
                Email = request.AdminEmail,
                FirstName = request.FirstName,
                LastName = request.LastName,
                OrganizationId = organization.Id,
                EmailConfirmed = true,
                IsUsingTemporaryPassword = false
            };

            var userResult = await _userManager.CreateAsync(adminUser, tempPassword);
            if (!userResult.Succeeded)
            {
                var errors = string.Join(", ", userResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create admin user: {errors}");
            }

            _logger.LogInformation("Organization admin user created successfully: {UserId}", adminUser.Id);

            try
            {
                var emailToken = await _userManager.GenerateEmailConfirmationTokenAsync(adminUser);

                var email = _emailBuilder.BuildTemporaryPasswordForOrganizationAdminEmail(
                    adminUser.Email,
                    adminUser.FirstName,
                    organization.Name,
                    tempPassword,
                    _settings.FrontendBaseUrl
                );

                await _emailSender.SendEmailAsync(email);

                _logger.LogInformation("Temporary password email sent to admin user: {UserId}", adminUser.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send confirmation email to user {UserId}", adminUser.Id);
                throw new InvalidOperationException("Failed to send confirmation email.");
            }

            var roleResult = await _userManager.AddToRoleAsync(adminUser, "OrganizationAdmin");
            if (!roleResult.Succeeded)
            {
                var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to assign role: {errors}");
            }

            return organization;
        }

        public async Task<Organization?> GetOrganizationByIdAsync(int organizationId)
        {
            return await _dbContext.Organizations.FindAsync(organizationId);
        }

        public PagedResult<Organization> GetPaginatedOrganizations(int page, int pageSize, string? search = null)
        {
            _logger.LogInformation("Attempting to retrieve paginated organizations from the database. Search: {Search}", search);

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var query = _dbContext.Organizations.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = $"%{search.Trim()}%";
                query = query.Where(o =>
                    EF.Functions.ILike(o.Name, term) ||
                    (!string.IsNullOrEmpty(o.Description) && EF.Functions.ILike(o.Description, term)));
            }

            var totalItems = query.Count();
            var items = query
                .OrderBy(o => o.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<Organization>(items, totalItems, page, pageSize);
        }

        public async Task<List<UsersResponse>> GetUsersByOrganizationIdAsync(int organizationId)
        {
            try
            {
                var usersWithRoles = await (from u in _dbContext.Users
                                            where u.OrganizationId == organizationId
                                            select new UsersResponse
                                            {
                                                Id = u.Id,
                                                UserName = u.UserName,
                                                FirstName = u.FirstName,
                                                LastName = u.LastName,
                                                EmailConfirmed = u.EmailConfirmed,
                                                Email = u.Email,
                                                Roles = (from ur in _dbContext.UserRoles
                                                         join r in _dbContext.Roles on ur.RoleId equals r.Id
                                                         where ur.UserId == u.Id
                                                         select r.Name).ToList()
                                            }).ToListAsync();

                if (!usersWithRoles.Any())
                {
                    _logger.LogWarning("No users found for OrganizationId={OrgId}", organizationId);
                    return [];
                }

                return usersWithRoles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while fetching users for OrganizationId={OrgId}", organizationId);
                throw;
            }
        }

        public async Task<(IEnumerable<Organization> Items, int TotalCount)>
         GetAllOrganizationsAsync(
             Role role,
             int organizationId,
             string? searchTerm,
             int pageNumber = 1,
             int pageSize = 10,
             CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Fetching organizations for role={Role}, orgId={OrgId}, search='{Search}', page={Page}, size={Size}",
                role, organizationId, searchTerm, pageNumber, pageSize);

            IQueryable<Organization> query = _dbContext.Organizations;

            query = role switch
            {
                Role.OrganizationAdmin => query.Where(o => o.Id == organizationId),
                Role.SuperAdmin => query,
                _ => query.Where(_ => false)
            };

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = $"%{searchTerm}%";
                query = query.Where(o =>
                    EF.Functions.ILike(o.Name, term) ||
                    EF.Functions.ILike(o.Description, term));
            }

            var totalCount = await query
                .CountAsync(cancellationToken)
                .ConfigureAwait(false);

            var items = await query
                .OrderBy(o => o.Name)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Returning {Count} of {TotalCount} organizations.",
                items.Count, totalCount);

            return (items, totalCount);
        }

        public async Task<IEnumerable<OrganizationUnpaged>> GetAllUnpagedOrganizationsAsync()
        {
            return await _dbContext.Organizations
                .Select(o => new OrganizationUnpaged
                {
                    Id = o.Id,
                    Name = o.Name
                })
                .ToListAsync();
        }

        public async Task<int> GetOrganizationsCountAsync()
        {
            return await _dbContext.Organizations.CountAsync();
        }

        public async Task<string[]> CreateNotificationEmailsAsync(int orgId, string[] emails)
        {
            var org = await _dbContext.Organizations.FindAsync(orgId)
                      ?? throw new KeyNotFoundException($"Org {orgId} not found");

            if (org.NotificationEmails?.Any() == true)
                throw new InvalidOperationException("Notifications already configured; use Update.");

            org.NotificationEmails = emails;
            await _dbContext.SaveChangesAsync();

            return org.NotificationEmails!;
        }
        public async Task<string[]> GetNotificationEmailsAsync(int orgId)
        {
            var org = await _dbContext.Organizations.FindAsync(orgId)
                      ?? throw new KeyNotFoundException($"Org {orgId} not found");

            return org.NotificationEmails ?? [];
        }

        public async Task<string[]> UpdateNotificationEmailsAsync(int orgId, string[] emails)
        {
            var org = await _dbContext.Organizations.FindAsync(orgId)
                      ?? throw new KeyNotFoundException($"Org {orgId} not found");

            org.NotificationEmails = emails;
            await _dbContext.SaveChangesAsync();

            return org.NotificationEmails!;
        }

        public async Task DeleteNotificationEmailsAsync(int orgId)
        {
            var org = await _dbContext.Organizations.FindAsync(orgId)
                      ?? throw new KeyNotFoundException($"Org {orgId} not found");

            if (org.NotificationEmails == null)
                return; 

            org.NotificationEmails = null;
            await _dbContext.SaveChangesAsync();
        }

        public async Task<int?> GetOrganizationIdByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be empty.", nameof(email));

            return await _dbContext.Organizations
                .Where(o => o.Email.ToLower() == email.ToLower())
                .Select(o => (int?)o.Id)
                .FirstOrDefaultAsync();
        }

        public async Task<Organization?> GetOrganizationByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be empty.", nameof(email));

            return await _dbContext.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Email.ToLower() == email.ToLower());
        }

        public async Task<List<object>> GetOrganizationNamesAsync(IEnumerable<int> organizationIds)
        {
            if (organizationIds == null || !organizationIds.Any())
                return [];

            var organizations = await _dbContext.Organizations
                .Where(o => organizationIds.Contains(o.Id))
                .Select(o => new
                {
                    id = o.Id,
                    organization_name = o.Name
                })
                .ToListAsync<object>();

            return organizations;
        }
    }
}
