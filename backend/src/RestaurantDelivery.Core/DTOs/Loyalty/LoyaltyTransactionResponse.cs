namespace RestaurantDelivery.Core.DTOs.Loyalty;

public class LoyaltyTransactionResponse
{
    public string CustomerId { get; set; } = string.Empty;
    public int PointsTransacted { get; set; }
    public int CurrentPoints { get; set; }
    public int TotalLifetimePoints { get; set; }
    public string MembershipTier { get; set; } = string.Empty;
    public bool TierUpgraded { get; set; }

    // Only meaningful for a redemption (points / 10).
    public decimal DiscountAmount { get; set; }
}
