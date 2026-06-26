namespace UsersService.Configuration
{
    public class JwtKeyOptions
    {
        public string? PrivateKeyPem { get; set; }
        public string? PublicKeyPem { get; set; }

        public string? PrivateKeyPath { get; set; }
        public string? PublicKeyPath { get; set; }
    }
}
