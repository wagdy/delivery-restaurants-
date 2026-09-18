namespace RestaurantDelivery.Core.DTOs.Orders;

public class OrderItemResponse
{
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public string MenuItemName { get; set; } = string.Empty;

    // The size/weight that was ordered, snapshotted at order time. Null for items with
    // no variants. The kitchen ticket and the customer's receipt both need it - "Mixed
    // Grill" alone does not say whether to cook 500g or a kilo.
    public string? VariantName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public List<OrderItemAddOnResponse> AddOns { get; set; } = new();
    public decimal LineTotal => Quantity * (UnitPrice + AddOns.Sum(a => a.Price));
}
