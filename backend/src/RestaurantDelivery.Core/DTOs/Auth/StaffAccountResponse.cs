using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.DTOs.Auth;

// One row in the Staff tab's management table (GET api/staff) - distinct from
// UserProfileResponse (which resolves Role/CustomRoleId into effective Modules/
// GranularPermissions for the currently-signed-in user) since this is about listing
// OTHER staff accounts for an admin to edit/delete, not resolving the caller's own access.
public class StaffAccountResponse
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public UserRole Role { get; set; }

    // Null for CaptainOrder, and for an Admin with no custom Role assigned (the
    // no-CustomRoleId "full access" default - see AuthService.ResolveAdminModuleNamesAsync).
    // Needed so the Staff tab's Edit action can pre-select the exact same three-way Role
    // control (Captain sentinel vs a specific Role id) the Create form already uses.
    public int? RoleId { get; set; }
    public string? RoleName { get; set; }
}
