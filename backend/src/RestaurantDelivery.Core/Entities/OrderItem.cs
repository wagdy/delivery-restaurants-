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

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public ICollection<OrderItemAddOn> AddOns { get; set; } = new List<OrderItemAddOn>();
}
