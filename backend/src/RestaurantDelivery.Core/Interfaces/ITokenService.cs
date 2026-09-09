using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Core.Interfaces;

public interface ITokenService
{
    // expiryOverride replaces the configured Jwt:ExpiryMinutes default when given - used by
    // AuthService.LoginAsync's RememberMe handling (30 days / 1 day). Null keeps the
    // existing config-driven expiry, so every other caller is unaffected.
    (string Token, DateTime ExpiresAtUtc) CreateToken(
        AppUser user,
        IReadOnlyList<string> adminModules,
        IReadOnlyList<string> granularPermissions,
        TimeSpan? expiryOverride = null);
}
