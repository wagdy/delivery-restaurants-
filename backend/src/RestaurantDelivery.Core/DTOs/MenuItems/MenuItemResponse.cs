using RestaurantDelivery.Core.DTOs.AddOns;

namespace RestaurantDelivery.Core.DTOs.MenuItems;

public class MenuItemResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Null when no Arabic name has been entered - the client falls back to Name.
    public string? NameAr { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
    public int? SubCategoryId { get; set; }
    // Denormalized alongside SubCategoryId, mirroring OrderItemResponse.MenuItemName -
    // saves every consumer (the storefront grouping, admin tables) a lookup join.
    public string? SubCategoryName { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; }

    // Always present but only ever true for callers that asked for deleted rows and
    // passed the Module.MenuItems check - the storefront never sees a true here.
    public bool IsDeleted { get; set; }
    public List<AddOnResponse> AddOns { get; set; } = new();

    // Empty for most items. When non-empty the client MUST make the customer choose one,
    // and Price above is a fallback nobody pays - the server refuses an order for an
    // item with variants that does not name one.
    public List<MenuItemVariantResponse> Variants { get; set; } = new();
}
