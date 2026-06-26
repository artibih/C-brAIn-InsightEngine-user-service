using Duende.IdentityServer.Models;
using UsersService.Models.IdentityServer;

namespace UsersService.IdentityConfig
{
    public static class Config
    {
        private static IdentityServerSettings GetSettings(IConfiguration configuration)
        {
            var settings = configuration.GetSection("IdentityServer").Get<IdentityServerSettings>();
            if (settings is null)
                throw new InvalidOperationException("IdentityServer section is missing from configuration.");

            settings.Clients ??= new();
            settings.ApiScopes ??= new();
            settings.ApiResources ??= new();

            return settings;
        }

        public static IEnumerable<ApiScope> GetApiScopes(IConfiguration configuration)
        {
            var settings = GetSettings(configuration);

            return settings.ApiScopes.Select(s =>
            {
                if (string.IsNullOrWhiteSpace(s.Name))
                    throw new InvalidOperationException("IdentityServer:ApiScopes has an entry with empty Name.");

                return new ApiScope(s.Name, s.DisplayName);
            }).ToList();
        }

        public static IEnumerable<Client> GetClients(IConfiguration configuration)
        {
            var settings = GetSettings(configuration);

            var requireSecrets = configuration.GetValue("IdentityServer:RequireClientSecrets", true);

            return settings.Clients.Select(c =>
            {
                if (string.IsNullOrWhiteSpace(c.ClientId))
                    throw new InvalidOperationException("IdentityServer:Clients has an entry with empty ClientId.");

                var needsSecret =
                    c.AllowedGrantTypes?.Contains("client_credentials") == true ||
                    c.AllowedGrantTypes?.Contains("dynamic_email") == true;

                if (needsSecret && requireSecrets && string.IsNullOrWhiteSpace(c.ClientSecret))
                {
                    throw new InvalidOperationException(
                        $"IdentityServer client '{c.ClientId}' is missing ClientSecret. " +
                        $"Provide it via env var: IdentityServer__Clients__N__ClientSecret");
                }

                var client = new Client
                {
                    ClientId = c.ClientId,
                    AllowedGrantTypes = c.AllowedGrantTypes,
                    AllowedScopes = c.AllowedScopes,
                    AlwaysSendClientClaims = c.AlwaysSendClientClaims,
                    Claims = c.Claims?.Select(x => new ClientClaim(x.Type, x.Value)).ToList()
                };

                if (!string.IsNullOrWhiteSpace(c.ClientSecret))
                {
                    client.ClientSecrets.Add(new Secret(c.ClientSecret.Sha256()));
                }

                return client;
            }).ToList();
        }


        public static IEnumerable<IdentityResource> GetIdentityResources()
        {
            return new List<IdentityResource>
            {
                new IdentityResources.OpenId(),
                new IdentityResources.Profile()
            };
        }

        public static IEnumerable<ApiResource> GetApiResources(IConfiguration configuration)
        {
            var settings = GetSettings(configuration);

            return settings.ApiResources.Select(r =>
            {
                if (string.IsNullOrWhiteSpace(r.Name))
                    throw new InvalidOperationException("IdentityServer:ApiResources has an entry with empty Name.");

                var apiResource = new ApiResource(r.Name, r.DisplayName ?? r.Name);

                foreach (var scope in r.Scopes.Where(s => !string.IsNullOrWhiteSpace(s)))
                    apiResource.Scopes.Add(scope);

                return apiResource;
            }).ToList();
        }
    }
}
