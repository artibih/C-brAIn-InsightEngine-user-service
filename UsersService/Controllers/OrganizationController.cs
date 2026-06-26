using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UsersService.Interfaces;
using UsersService.Models.Entities;
using UsersService.Models.Entities.Projections;
using UsersService.Models.Requests.Organizations;
using UsersService.Util;

namespace UsersService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class OrganizationController : ControllerBase
    {

        private readonly IOrganizationService _organizationService;
        private readonly ILogger<OrganizationController> _logger;

        public OrganizationController(
            IOrganizationService organizationService,
            ILogger<OrganizationController> logger)
        {
            _organizationService = organizationService;
            _logger = logger;
        }

        [HttpPost]
        [Authorize(Roles = "Admin, SuperAdmin")]
        public async Task<ActionResult<Organization>> CreateOrganization([FromBody] CreateOrganizationRequest request)
        {
            try
            {
                var createdOrg = await _organizationService.CreateOrganizationWithAdminAsync(request);

                return CreatedAtAction(
                    nameof(GetOrganizationById),
                    new { organizationId = createdOrg.Id },
                    createdOrg
                );
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Validation failed during organization creation.");
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create a new organization.");
                return StatusCode(500, "An error occurred while creating the organization.");
            }
        }


        [HttpGet("{organizationId}")]
        public async Task<ActionResult<Organization>> GetOrganizationById(int organizationId)
        {
            try
            {
                var org = await _organizationService.GetOrganizationByIdAsync(organizationId);
                if (org == null)
                {
                    return NotFound($"Organization with Id={organizationId} was not found.");
                }
                return Ok(org);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve organization by ID: {Id}.", organizationId);
                return StatusCode(500, "An error occurred while retrieving the organization.");
            }
        }

        [HttpGet("DashboardPaged")]
        public ActionResult GetAllOrganizations(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string search = null)
        {
            try
            {
                var pagedResult = _organizationService.GetPaginatedOrganizations(page, pageSize, search);

                var response = new
                {
                    page = pagedResult.Page,
                    pageSize = pagedResult.PageSize,
                    totalItems = pagedResult.TotalItems,
                    totalPages = pagedResult.TotalPages,
                    items = pagedResult.Items
                };

                return Ok(new { data = response });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve paginated organizations.");
                return StatusCode(500, "An error occurred while retrieving organizations.");
            }
        }


        [HttpGet]
        public async Task<ActionResult> GetAllOrganizationsAsync(
        [FromQuery] string? searchTerm,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
        {
            try
            {
                var role = User.GetUserRole();
                var orgId = int.Parse(User.GetOrgId());

                _logger.LogInformation(
                    "HTTP GET: role={Role}, orgId={OrgId}, search='{Search}', page={Page}, size={Size}",
                    role, orgId, searchTerm, pageNumber, pageSize);

                var (items, totalCount) = await _organizationService
                    .GetAllOrganizationsAsync(
                        role,
                        orgId,
                        searchTerm,
                        pageNumber,
                        pageSize,
                        cancellationToken);

                var response = new
                {
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    Organizations = items
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve paginated organizations.");
                return StatusCode(500, "An error occurred while retrieving organizations.");
            }
        }

        [HttpGet("Unpaged")]
        public async Task<ActionResult<IEnumerable<OrganizationUnpaged>>> GetAllUnpagedOrganization()
        {
            var organizations = await _organizationService.GetAllUnpagedOrganizationsAsync();
            return Ok(organizations);
        }

        [HttpGet("public-list")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<OrganizationUnpaged>>> GetPublicOrganizationList()
        {
            var organizations = await _organizationService.GetAllUnpagedOrganizationsAsync();
            return Ok(organizations);
        }

        [HttpGet("count")]
        public async Task<IActionResult> GetOrganizationsCountAsync()
        {
            var count = await _organizationService.GetOrganizationsCountAsync();
            return Ok(count);
        }

        [HttpGet("users/{organizationId}")]
        public async Task<IActionResult> GetUsersByOrganizationId(int organizationId)
        {
            try
            {
                var users = await _organizationService.GetUsersByOrganizationIdAsync(organizationId);

                if (users == null || !users.Any())
                {
                    _logger.LogWarning("No users found for OrganizationId={OrgId}", organizationId);
                    return NotFound(new { Message = "No users found for this organization." });
                }

                return Ok(users);
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while processing your request.");
            }
        }

        [HttpPost("NotificationEmails")]
        [Authorize(Roles = "SuperAdmin,Admin,OrganizationAdmin")]
        public async Task<IActionResult> CreateNotificationEmails(
            [FromBody] CreateNotificationEmailsRequest req)
        {
            var orgId = req.OrganizationId ?? int.Parse(User.GetOrgId());

            try
            {
                var emails = await _organizationService.CreateNotificationEmailsAsync(orgId, req.NotificationEmails);
                return Ok(new { Success = true, Data = emails });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpGet("NotificationEmails")]
        [Authorize(Roles = "SuperAdmin,Admin,OrganizationAdmin")]
        public async Task<IActionResult> GetNotificationEmails([FromQuery] int? organizationId = null)
        {
            var orgId = organizationId ?? int.Parse(User.GetOrgId());

            try
            {
                var emails = await _organizationService.GetNotificationEmailsAsync(orgId);
                return Ok(new { Success = true, Data = emails });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching notification emails.", detail = ex.Message });
            }
        }


        [HttpPut("NotificationEmails")]
        public async Task<IActionResult> UpdateNotificationEmails(
            [FromBody] UpdateNotificationEmailsRequest req)
        {
            var orgId = req.OrganizationId ?? int.Parse(User.GetOrgId());

            try
            {
                var emails = await _organizationService.UpdateNotificationEmailsAsync(orgId, req.NotificationEmails);
                return Ok(new { Success = true, Data = emails });
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpDelete("NotificationEmails")]
        [Authorize(Roles = "SuperAdmin,Admin,OrganizationAdmin")]

        public async Task<IActionResult> DeleteNotificationEmails(
            [FromQuery] int? organizationId)
        {
            var orgId = organizationId ?? int.Parse(User.GetOrgId());

            await _organizationService.DeleteNotificationEmailsAsync(orgId);
            return NoContent();
        }

        [HttpGet("GetOrganizationIdByEmail")]
        public async Task<IActionResult> GetOrganizationIdByEmail([FromQuery] string email)
        {
            try
            {
                _logger.LogInformation("HTTP GET: Retrieving organization ID by email: {Email}.", email);

                if (string.IsNullOrWhiteSpace(email))
                {
                    _logger.LogWarning("Email parameter was missing or empty in GetOrganizationIdByEmail request.");
                    return BadRequest(new { message = "Email is required." });
                }

                var orgId = await _organizationService.GetOrganizationIdByEmailAsync(email);

                if (orgId is null)
                {
                    _logger.LogWarning("No organization found for email: {Email}.", email);
                    return NotFound(new { message = $"No organization found for email {email}." });
                }

                return Ok(new { organizationId = orgId });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetOrganizationIdByEmail: {Email}.", email);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving organization ID for email: {Email}.", email);
                return StatusCode(500, new { message = "An error occurred while retrieving the organization ID." });
            }
        }


        [HttpGet("GetOrganizationByEmail")]
        public async Task<IActionResult> GetOrganizationByEmail([FromQuery] string email)
        {
            try
            {
                _logger.LogInformation("HTTP GET: Retrieving organization by email: {Email}.", email);

                if (string.IsNullOrWhiteSpace(email))
                {
                    _logger.LogWarning("Email parameter was missing or empty in GetOrganizationByEmail request.");
                    return BadRequest(new { message = "Email is required." });
                }

                var organization = await _organizationService.GetOrganizationByEmailAsync(email);

                if (organization == null)
                {
                    _logger.LogWarning("No organization found for email: {Email}.", email);
                    return NotFound(new { message = $"No organization found for email {email}." });
                }

                return Ok(organization);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument provided for GetOrganizationByEmail: {Email}.", email);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving organization for email: {Email}.", email);
                return StatusCode(500, new { message = "An error occurred while retrieving the organization." });
            }
        }

        [HttpPost("names")]
        public async Task<IActionResult> GetOrganizationNames([FromBody] List<int> organizationIds)
        {
            if (organizationIds == null || organizationIds.Count == 0)
                return BadRequest("Organization IDs are required.");

            var result = await _organizationService.GetOrganizationNamesAsync(organizationIds);

            if (result == null || result.Count == 0)
                return NotFound("No organizations found for provided IDs.");

            return Ok(result);
        }

    }
}
