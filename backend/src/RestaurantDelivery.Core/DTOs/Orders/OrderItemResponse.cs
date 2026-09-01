namespace RestaurantDelivery.Core.DTOs.Orders;

public class OrderItemResponse
{
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public string MenuItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public List<OrderItemAddOnResponse> AddOns { get; set; } = new();
    public decimal LineTotal => Quantity * (UnitPrice + AddOns.Sum(a => a.Price));
}
