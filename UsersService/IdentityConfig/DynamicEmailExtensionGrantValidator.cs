using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using UsersService.Models.Entities;

namespace UsersService.IdentityConfig
{
    public class DynamicEmailExtensionGrantValidator : IExtensionGrantValidator
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public DynamicEmailExtensionGrantValidator(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public string GrantType => "dynamic_email";

        public async Task ValidateAsync(ExtensionGrantValidationContext context)
        {
            var email = context.Request.Raw.Get("email");
            if (string.IsNullOrWhiteSpace(email))
            {
                context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest, "Email is missing.");
                return;
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "User not found.");
                return;
            }

            if (!user.OrganizationId.HasValue)
            {
                throw new InvalidOperationException("Organization ID is required but missing.");
            }

            string organizationId = user.OrganizationId.Value.ToString();

            var claims = new List<Claim>
        {
            new ("unique_name", user.Email),
            new ("groupsid", organizationId)
        };


            context.Result = new GrantValidationResult(
                subject: user.Id.ToString(),
                authenticationMethod: GrantType,
                claims: claims);
        }
    }
}
