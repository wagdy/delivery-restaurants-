namespace RestaurantDelivery.Core.DTOs.Loyalty;

public class CustomerCampaignProgressResponse
{
    public Guid CampaignId { get; set; }
    public string CampaignTitle { get; set; } = string.Empty;
    public string CampaignDescription { get; set; } = string.Empty;
    public int TargetPunches { get; set; }
    public int CurrentPunches { get; set; }
    public int RewardsEarned { get; set; }
    public DateTime? EndDate { get; set; }
}
