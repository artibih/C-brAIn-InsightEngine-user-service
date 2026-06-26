namespace UsersService.Models.IdentityServer
{
    public class ClientSettings
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public List<string> AllowedGrantTypes { get; set; }
        public List<string> AllowedScopes { get; set; }
        public List<ClientClaimSettings> Claims { get; set; } = new();
        public bool AlwaysSendClientClaims { get; set; }
    }
}
