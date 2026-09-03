namespace RestaurantDelivery.Core.DTOs.Loyalty;

// One campaign's punch outcome from a single order, used both in OrderLoyaltyResult and to
// build the WhatsApp order-confirmation message body.
public class PunchUpdateSummary
{
    public string CampaignTitle { get; set; } = string.Empty;
    public int QuantityApplied { get; set; }
    public int CurrentPunches { get; set; }
    public int TargetPunches { get; set; }
    public int RewardsEarnedThisOrder { get; set; }
}
