using UsersService.Models.Entities;
using UsersService.Models.Helpers;
using UsersService.Models.Requests.Reports;
using UsersService.Models.Requests.Users;
using UsersService.Models.Responses.Reports;
using UsersService.Models.Responses.Users;

namespace UsersService.Interfaces
{
    public interface IUserService
    {
        Task<ProblemReportResponse> CreateProblemReportAsync(int reporterId, ProblemReportRequest request);
        Task<int> GetUsersCountAsync();
        Task<PagedResult<UsersResponse>> GetPaginatedUsersAsync(
            string? role,
            int userId,
            int orgId,
            int page,
            int pageSize,
            string? search,
            CancellationToken ct = default);
        Task<ApplicationUser> CreateUserAsync(CreateUserRequest request, ApplicationUser currentUser, CancellationToken ct);
        Task<bool> ApproveUserAsync(int id, CancellationToken ct = default);
        Task UpdateUserAsync(int id, UpdateUserRequest request);
        Task DeleteUserAsync(int id, ApplicationUser currentUser);
        Task<UsersResponse?> GetUserByIdAsync(int id, CancellationToken ct = default);
        Task UpdateUserAdminAsync(int id, UpdateUserAdminRequest request, ApplicationUser currentUser);
    }
}