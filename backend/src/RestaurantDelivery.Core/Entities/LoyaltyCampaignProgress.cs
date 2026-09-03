namespace RestaurantDelivery.Core.Entities;

// One row per (customer, campaign) pair, created lazily on first punch - there is no
// separate "enroll" action, matching the source app's model.
public class LoyaltyCampaignProgress
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string CustomerId { get; set; } = string.Empty;

    public AppUser Customer { get; set; } = null!;

    public Guid CampaignId { get; set; }

    public LoyaltyCampaign Campaign { get; set; } = null!;

    public int CurrentPunches
    {
        get;
        set => field = value < 0 ? 0 : value;
    }

    // A counter, not a boolean - a customer can bank more than one unredeemed reward.
    public int RewardsEarned
    {
        get;
        set => field = value < 0 ? 0 : value;
    }

    public DateTime? LastPunchDate { get; set; }
}
