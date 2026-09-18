using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.MenuItems;

public class MenuItemVariantResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Null when no Arabic name has been entered - the client falls back to Name.
    public string? NameAr { get; set; }

    // Absolute, not a delta on the item's base price. See MenuItemVariant.
    public decimal Price { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsAvailable { get; set; }
}

public class MenuItemVariantRequest
{
    // Present when editing an existing variant, absent (0/null) when adding a new one.
    // MenuItemService matches on it so that editing a size does not orphan the order
    // history's VariantId, and so unlisted variants can be removed in one pass.
    public int? Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? NameAr { get; set; }

    [Range(0.01, 100000)]
    public decimal Price { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsAvailable { get; set; } = true;
}
