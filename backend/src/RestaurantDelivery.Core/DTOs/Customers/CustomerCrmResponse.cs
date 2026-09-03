namespace RestaurantDelivery.Core.DTOs.Customers;

// Unlike CustomerInsightResponse (order stats only), this includes an Id (for scanner/
// history lookups) and the customer's loyalty standing - requires its own endpoint since
// GetCustomerInsightsAsync never joins LoyaltyProfiles.
public class CustomerCrmResponse
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public int TotalOrders { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int CurrentPoints { get; set; }
    public int TotalLifetimePoints { get; set; }
    public string MembershipTier { get; set; } = string.Empty;
}
