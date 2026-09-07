using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.DTOs.Auth;

public class UserProfileResponse
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public UserRole Role { get; set; }

    // Effective admin module names granted to this user - null for Customer/CaptainOrder,
    // and for Admin means "resolved at login" (all 5 modules if CustomRoleId is null, else
    // the assigned Role's modules). See AuthService.ResolveAdminModuleNamesAsync.
    public List<string>? Modules { get; set; }

    // Granular sub-permissions (e.g. "Orders.Create") within the granted Modules above -
    // null for Customer/CaptainOrder; for Admin, empty/absent for a module means "no
    // restriction recorded", full access to everything under it. See
    // AuthService.ResolveGranularPermissionNamesAsync.
    public List<string>? GranularPermissions { get; set; }
}
