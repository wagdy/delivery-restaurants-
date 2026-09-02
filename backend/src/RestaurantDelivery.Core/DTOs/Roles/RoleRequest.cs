using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Roles;

public class RoleRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public List<string> Modules { get; set; } = new();
}
