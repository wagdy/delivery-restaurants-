namespace RestaurantDelivery.Core.Entities;

// Admin-managed catalog entry (e.g. "Extra Sauce", "Side Rice"), assigned
// per menu item via MenuItemAddOn rather than being available on every item.
public class AddOn
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }

    public ICollection<MenuItemAddOn> MenuItemAddOns { get; set; } = new List<MenuItemAddOn>();
}
