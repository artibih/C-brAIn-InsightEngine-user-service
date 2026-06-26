namespace UsersService.Models.IdentityServer
{
    public class IdentityServerSettings
    {
        public List<ClientSettings> Clients { get; set; }
        public List<ApiScopeSettings> ApiScopes { get; set; }
        public List<ApiResourceSettings> ApiResources { get; set; } = new();
        public bool RequireClientSecrets { get; set; } = false;
    }
}
