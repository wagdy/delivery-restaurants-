namespace RestaurantDelivery.Core.Entities;

// A punch-card campaign, e.g. "Buy 5 Coffees, Get 1 Free". CategoryName is matched
// case-insensitively against MenuItem.Category (a free-text field, not a real FK) when an
// order is delivered - see LoyaltyService.ProcessOrderDeliveredAsync. Null CategoryName
// means every order item counts (a "buy N of anything" campaign).
public class LoyaltyCampaign
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? CategoryName { get; set; }

    // Field-backed setter: clamped to a floor of 1, mirroring the source app's guard
    // against a nonsensical zero/negative target.
    public int TargetPunches
    {
        get;
        set => field = value < 1 ? 1 : value;
    } = 1;

    public bool IsActive { get; set; } = true;

    // Stamped whenever IsActive flips true (including on creation).
    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
