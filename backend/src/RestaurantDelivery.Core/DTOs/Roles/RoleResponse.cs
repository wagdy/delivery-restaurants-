namespace RestaurantDelivery.Core.DTOs.Roles;

public class RoleResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> Modules { get; set; } = new();
}
