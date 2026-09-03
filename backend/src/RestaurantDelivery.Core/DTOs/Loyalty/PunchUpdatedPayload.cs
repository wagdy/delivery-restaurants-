namespace RestaurantDelivery.Core.DTOs.Loyalty;

// Pushed over SignalR (LoyaltyHub, event "PunchUpdated") to the affected customer's own
// connections.
public class PunchUpdatedPayload
{
    public Guid CampaignId { get; set; }
    public string CampaignTitle { get; set; } = string.Empty;
    public int CurrentPunches { get; set; }
    public int TargetPunches { get; set; }
    public int RewardsEarned { get; set; }
    public bool RewardEarnedThisPunch { get; set; }
}
