namespace UsersService.Models.IdentityServer
{
    public class ApiResourceSettings
    {
        public string Name { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public List<string> Scopes { get; set; } = new();
    }
}
