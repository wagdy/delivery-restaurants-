namespace RestaurantDelivery.Core.Entities;

public class MenuItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Optional Arabic display name. Null/blank means "no Arabic name was entered", and
    // the client falls back to Name - see LocalNamePipe on the frontend. Deliberately a
    // separate nullable column rather than a translations table: the app has exactly two
    // languages and one translatable field per row, and a join table would buy nothing
    // but a join. Note that Name stays the identity used for lookups and grouping
    // (MenuItem.Category matches Category.Name as free text), so this column is display
    // only - nothing keys off it.
    public string? NameAr { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; } = true;

    // Optional finer-grained grouping within Category above (e.g. "Hot Drinks" inside
    // "Drinks") - unlike Category, this is a real FK, not free text, since SubCategory
    // only exists as a row created through the SubCategories API.
    public int? SubCategoryId { get; set; }
    public SubCategory? SubCategory { get; set; }

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<MenuItemAddOn> MenuItemAddOns { get; set; } = new List<MenuItemAddOn>();
}
