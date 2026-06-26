namespace UsersService.Configuration
{
    public class CookieOptions
    {
        public double ExpirationDays { get; set; } = 14;
        public bool SlidingExpiration { get; set; } = true;
        public string SameSite { get; set; } = "Lax";
        public string SecurePolicy { get; set; } = "Always";
        public bool HttpOnly { get; set; } = true;
    }
}
