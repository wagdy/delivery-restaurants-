using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.Entities;

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public AdminModules Modules { get; set; }
}
