namespace RestaurantDelivery.Core.DTOs.Loyalty;

public class RedeemRewardResult
{
    public string CustomerId { get; set; } = string.Empty;
    public Guid CampaignId { get; set; }
    public string CampaignTitle { get; set; } = string.Empty;
    public int RewardsEarned { get; set; }
}
