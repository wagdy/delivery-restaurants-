namespace RestaurantDelivery.Core.DTOs.Loyalty;

// Pushed over SignalR (LoyaltyHub, event "PointsUpdated") to the affected customer's own
// connections. MembershipTier is pre-stringified rather than the raw enum - AddSignalR()
// and AddControllers().AddJsonOptions() use two independent System.Text.Json configs, so
// the REST API's JsonStringEnumConverter doesn't apply to hub messages.
public class PointsUpdatedPayload
{
    public int CurrentPoints { get; set; }
    public int TotalLifetimePoints { get; set; }
    public string MembershipTier { get; set; } = string.Empty;
    public bool TierUpgraded { get; set; }
}
