namespace RestaurantDelivery.Core.Entities;

// A named grouping inside one Category (e.g. "Hot Drinks" inside "Drinks"), rendered as
// an inline section headline on the storefront - never its own clickable card, unlike
// Category itself. Unlike MenuItem.Category (a free-text field, see that entity's own
// comment), this is a real foreign-key relationship: a MenuItem can only be tagged with
// a SubCategory that actually exists.
public class SubCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
}
