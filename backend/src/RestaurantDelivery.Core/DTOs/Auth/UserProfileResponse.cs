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
}
