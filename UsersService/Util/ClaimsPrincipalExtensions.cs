using System.Security.Claims;
using UsersService.Enums;
namespace UsersService.Util
{
    public static class ClaimsPrincipalExtensions
    {
        public static string GetUserId(this ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                throw new UnauthorizedAccessException("UserId claim is missing.");
            }
            if (!int.TryParse(userIdClaim, out int userId))
            {
                throw new UnauthorizedAccessException("Invalid UserId.");
            }
            return userId.ToString();
        }

        public static string GetOrgId(this ClaimsPrincipal org)
        {
            var orgIdClaim = org.FindFirst(ClaimTypes.GroupSid)?.Value;
            if (string.IsNullOrEmpty(orgIdClaim))
            {
                throw new UnauthorizedAccessException("OrganizationId claim is missing.");
            }
            if (!int.TryParse(orgIdClaim, out int organizationId))
            {
                throw new UnauthorizedAccessException("Invalid OrganizationId.");
            }
            return organizationId.ToString();
        }

        public static Role GetUserRole(this ClaimsPrincipal user)
        {
            var roleClaim = user.FindFirst(ClaimTypes.Role)?.Value;
            if (string.IsNullOrEmpty(roleClaim))
            {
                throw new UnauthorizedAccessException("Role claim is missing.");
            }

            if (!Enum.TryParse<Role>(roleClaim, ignoreCase: true, out var role))
            {
                throw new UnauthorizedAccessException($"Invalid role value: {roleClaim}");
            }

            return role;
        }
    }
}