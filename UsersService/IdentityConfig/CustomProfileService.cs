using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using System.Security.Claims;

namespace UsersService.IdentityConfig
{
    public class CustomProfileService : IProfileService
    {
        public Task GetProfileDataAsync(ProfileDataRequestContext context)
        {
            var claims = context.Subject.Claims;

            var issuedClaims = new List<Claim>();
            foreach (var claim in claims)
            {
                if (claim.Type == "unique_name" || claim.Type == "groupsid")
                {
                    claim.Properties["destinations"] = "access_token";
                }
                issuedClaims.Add(claim);
            }

            context.IssuedClaims = issuedClaims;
            return Task.CompletedTask;
        }

        public Task IsActiveAsync(IsActiveContext context)
        {
            context.IsActive = true;
            return Task.CompletedTask;
        }
    }
}
