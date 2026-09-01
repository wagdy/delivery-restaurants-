namespace RestaurantDelivery.Core.DTOs.Customers;

public class CustomerInsightResponse
{
    public string CustomerName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public int TotalOrders { get; set; }
    public decimal AverageOrderValue { get; set; }
}
