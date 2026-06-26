namespace UsersService.Configuration
{
    public class CacheOptions
    {
        public Dictionary<string, CacheItemOptions> Caches { get; set; } = new();
    }
}
