namespace UsersService.Configuration
{
    public class JwtSettings
    {
        public string[] Issuers { get; set; }
        public string[] Audiences { get; set; }
        public string Issuer { get; set; }
        public string Audience { get; set; }
        public int AccessExpirationInMinutes { get; set; }
        public int RefreshExpirationInMinutes { get; set; }
    }
}