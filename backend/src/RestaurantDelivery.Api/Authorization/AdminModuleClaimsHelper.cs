using System.Security.Claims;
using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Api.Authorization;

// Shared by PermissionAuthorizationHandler and OrdersAccessAuthorizationHandler so the
// "no modules claim at all -> fail open" rule (for tokens issued before this feature
// shipped, or any other edge case) lives in exactly one place.
public static class AdminModuleClaimsHelper
{
    public static bool HasModule(ClaimsPrincipal user, AdminModules module)
    {
        var moduleClaims = user.FindAll(AdminModuleClaims.ClaimType).ToList();
        if (moduleClaims.Count == 0)
        {
            return true;
        }

        return moduleClaims.Any(c => c.Value == module.ToString());
    }
}
