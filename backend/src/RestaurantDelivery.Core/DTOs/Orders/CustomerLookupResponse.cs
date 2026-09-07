namespace RestaurantDelivery.Core.DTOs.Orders;

// For the admin "Create Order" screen's registered-customer phone search - just enough
// to pre-fill the order form, not the full CustomerAnalyticsResponse (points/tier/etc.
// would be irrelevant noise here).
public class CustomerLookupResponse
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Address { get; set; }
}
