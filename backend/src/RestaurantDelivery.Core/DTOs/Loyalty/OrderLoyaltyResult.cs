namespace RestaurantDelivery.Core.DTOs.Loyalty;

// Returned by ILoyaltyService.ProcessOrderDeliveredAsync - feeds the WhatsApp
// order-confirmation message. PointsEarned is 0 (not an error) for a guest order or one
// too small to earn a point at the current CurrencyPerPoint ratio.
public class OrderLoyaltyResult
{
    public int PointsEarned { get; set; }
    public int NewTotalPoints { get; set; }
    public bool TierUpgraded { get; set; }
    public List<PunchUpdateSummary> PunchUpdates { get; set; } = [];
}
