using RestaurantDelivery.Core.DTOs.AddOns;

namespace RestaurantDelivery.Core.DTOs.MenuItems;

public class MenuItemResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; }
    public List<AddOnResponse> AddOns { get; set; } = new();
}
