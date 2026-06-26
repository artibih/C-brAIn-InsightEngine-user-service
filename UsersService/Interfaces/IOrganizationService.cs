using UsersService.Enums;
using UsersService.Models.Entities;
using UsersService.Models.Entities.Projections;
using UsersService.Models.Helpers;
using UsersService.Models.Requests.Organizations;
using UsersService.Models.Responses.Users;

namespace UsersService.Interfaces
{
    public interface IOrganizationService
    {
        Task<Organization> CreateOrganizationWithAdminAsync(CreateOrganizationRequest request);
        Task<Organization?> GetOrganizationByIdAsync(int organizationId);
        Task<List<UsersResponse>> GetUsersByOrganizationIdAsync(int organizationId);
        PagedResult<Organization> GetPaginatedOrganizations(int page, int pageSize, string? search = null);
        Task<IEnumerable<OrganizationUnpaged>> GetAllUnpagedOrganizationsAsync();
        Task<int> GetOrganizationsCountAsync();
        Task<(IEnumerable<Organization> Items, int TotalCount)> GetAllOrganizationsAsync(Role role, int organizationId, string? searchTerm, int pageNumber = 1, int pageSize = 10, CancellationToken cancellationToken = default);
        Task<string[]> CreateNotificationEmailsAsync(int orgId, string[] emails);
        Task<string[]> GetNotificationEmailsAsync(int orgId);
        Task<string[]> UpdateNotificationEmailsAsync(int orgId, string[] emails);
        Task DeleteNotificationEmailsAsync(int orgId);
        Task<Organization?> GetOrganizationByEmailAsync(string email);
        Task<int?> GetOrganizationIdByEmailAsync(string email);
        Task<List<object>> GetOrganizationNamesAsync(IEnumerable<int> organizationIds);
    }
}
