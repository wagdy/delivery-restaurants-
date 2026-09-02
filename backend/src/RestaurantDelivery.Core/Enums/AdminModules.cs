namespace RestaurantDelivery.Core.Enums;

[Flags]
public enum AdminModules
{
    None = 0,
    Orders = 1 << 0,
    MenuItems = 1 << 1,
    Settings = 1 << 2,
    Staff = 1 << 3,
    Customers = 1 << 4
}

// Single source of truth for the JWT claim type carrying granted admin modules - the
// writer (JwtTokenService) and every reader (PermissionAuthorizationHandler,
// OrdersAccessAuthorizationHandler) must agree on this exact string.
public static class AdminModuleClaims
{
    public const string ClaimType = "modules";
}
