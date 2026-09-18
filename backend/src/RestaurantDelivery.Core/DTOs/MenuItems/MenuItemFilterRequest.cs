namespace RestaurantDelivery.Core.DTOs.MenuItems;

public class MenuItemFilterRequest
{
    public string? SearchQuery { get; set; }
    public int? CategoryId { get; set; }
    public bool? IsAvailable { get; set; }
    public bool? HasAddons { get; set; }

    // Defaults to Active, so every existing caller - the storefront included - keeps
    // seeing exactly what it saw before soft delete existed.
    public DeletedFilter Deleted { get; set; } = DeletedFilter.Active;
}
