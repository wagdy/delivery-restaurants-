namespace RestaurantDelivery.Core.DTOs.Loyalty;

public class PunchResult
{
    public string CustomerId { get; set; } = string.Empty;
    public Guid CampaignId { get; set; }
    public string CampaignTitle { get; set; } = string.Empty;
    public int CurrentPunches { get; set; }
    public int TargetPunches { get; set; }
    public int RewardsEarned { get; set; }
    public bool RewardEarnedThisPunch { get; set; }
}
