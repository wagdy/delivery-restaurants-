namespace RestaurantDelivery.Core.Entities;

public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;

    // Snapshotted at order time, exactly like OrderItemAddOn.Name and UnitPrice below.
    // Until now the receipt read oi.MenuItem.Name through the navigation, which meant a
    // historical order could only be displayed while its menu item still existed and was
    // still visible to the query. Soft delete breaks precisely that: a global query
    // filter applies to included navigations too, so oi.MenuItem would come back null
    // and every past order containing a deleted item would throw on load.
    public string MenuItemName { get; set; } = string.Empty;

    // Which size/weight was ordered. Null for items that have no variants.
    //
    // The NAME is snapshotted for the same reason MenuItemName above is: a variant is
    // cascade-deleted with its menu item, so reading it back through a navigation would
    // lose the receipt. VariantId is kept only for reporting ("how many 1-Kilo trays did
    // we sell") and is deliberately not what the receipt renders.
    public int? VariantId { get; set; }
    public string? VariantName { get; set; }

    public int Quantity { get; set; }

    // Already the RESOLVED price: the chosen variant's price when there is one, the menu
    // item's base price otherwise. Nothing downstream has to know which case it was.
    public decimal UnitPrice { get; set; }

    public ICollection<OrderItemAddOn> AddOns { get; set; } = new List<OrderItemAddOn>();
}
