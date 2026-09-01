namespace RestaurantDelivery.Core.DTOs.MenuItems;

public class MenuItemFilterRequest
{
    public string? SearchQuery { get; set; }
    public int? CategoryId { get; set; }
    public bool? IsAvailable { get; set; }
    public bool? HasAddons { get; set; }
}
