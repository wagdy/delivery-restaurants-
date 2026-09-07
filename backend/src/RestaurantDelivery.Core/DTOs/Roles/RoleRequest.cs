using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Roles;

public class RoleRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public List<string> Modules { get; set; } = new();

    // Sub-permission strings, e.g. "Orders.Create" - empty means "no restriction", full
    // access to every sub-permission under each granted module. See
    // RestaurantDelivery.Core.Common.GranularPermissions for the valid catalog.
    public List<string> GranularPermissions { get; set; } = new();
}
