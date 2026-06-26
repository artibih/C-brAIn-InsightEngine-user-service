using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using UsersService.Configuration;
using UsersService.Interfaces;
using UsersService.Models.Entities;

namespace UsersService.Services
{
    public class UserRoleCache : IUserRoleCache
    {
        private readonly IMemoryCache _cache;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly TimeSpan _ttl;

        public UserRoleCache(
            IMemoryCache cache,
            UserManager<ApplicationUser> userManager,
            IOptions<CacheOptions> options)
        {
            _cache = cache;
            _userManager = userManager;

            _ttl = options.Value.Caches.TryGetValue("Roles", out var roleCache) ? TimeSpan.FromMinutes(roleCache.TtlMinutes > 0 ? roleCache.TtlMinutes : 2) : TimeSpan.FromMinutes(2);
        }

        public async Task<IReadOnlyList<string>> GetRolesAsync(ApplicationUser user, CancellationToken ct)
        {
            var cacheKey = $"user:roles:{user.Id}";

            if (_cache.TryGetValue<IReadOnlyList<string>>(cacheKey, out var roles))
            {
                return roles;
            }

            var dbRoles = await _userManager.GetRolesAsync(user);
            var rolesList = dbRoles.ToList();

            _cache.Set(
                cacheKey,
                rolesList,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _ttl
                });

            return rolesList;
        }
    }

}

