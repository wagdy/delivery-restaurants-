using Microsoft.AspNetCore.Identity;
using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.Entities;

public class AppUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public UserRole Role { get; set; } = UserRole.Customer;

    // Named CustomRole/CustomRoleId (not Role/RoleId) to avoid colliding with the
    // property above - Role here is the coarse Admin/CaptainOrder/Customer enum that
    // drives the JWT role claim and every [Authorize(Roles=...)] check; CustomRole is an
    // optional, finer-grained permission set that only narrows what an Admin account can
    // do (null means full access - see AuthService.ResolveAdminModuleNamesAsync).
    public int? CustomRoleId { get; set; }
    public Role? CustomRole { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
