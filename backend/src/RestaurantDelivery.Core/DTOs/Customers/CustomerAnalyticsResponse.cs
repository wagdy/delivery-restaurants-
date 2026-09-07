namespace RestaurantDelivery.Core.DTOs.Customers;

// Replaces the old, separate CustomerCrmResponse (loyalty-focused) and
// CustomerInsightResponse (order-stats-only) - the merged "Customer Insights" dashboard
// needs both in one row instead of two admin pages showing an overlapping-but-different
// slice of the same customer.
public class CustomerAnalyticsResponse
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    // Phone number when the customer has one (true for every customer who registered
    // normally - see AuthService.RegisterAsync), falling back to email only for the
    // rare account that somehow has none.
    public string ContactInfo { get; set; } = string.Empty;

    public int TotalPoints { get; set; }
    public int TotalOrders { get; set; }
    public decimal AverageCheck { get; set; }
    public bool IsPunchCardEnrolled { get; set; }
    public int PunchCardRedeemsCount { get; set; }
    public decimal TotalLifetimeValue { get; set; }
    public DateTime? LastOrderDate { get; set; }

    // Beyond the requested field set, kept because they're already resolved by the same
    // left join at zero extra query cost and were both already visible on the old CRM
    // screen - dropping them silently would be a regression from the page this replaces.
    public int CurrentPoints { get; set; }
    public string MembershipTier { get; set; } = string.Empty;
}
