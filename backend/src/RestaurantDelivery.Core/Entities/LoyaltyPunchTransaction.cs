using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.Entities;

public class LoyaltyPunchTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProgressId { get; set; }

    public LoyaltyCampaignProgress Progress { get; set; } = null!;

    // The staff member who performed a manual Scanner punch/redemption - null for a punch
    // applied automatically by ProcessOrderDeliveredAsync.
    public string? AdminId { get; set; }

    public AppUser? Admin { get; set; }

    // Set only for an order-triggered punch. A unique index on (OrderId, ProgressId) (see
    // LoyaltyPunchTransactionConfiguration) prevents the same order from punching the same
    // campaign twice if its Delivered status is somehow set more than once.
    public int? OrderId { get; set; }

    public Order? Order { get; set; }

    public PunchTransactionType TransactionType { get; set; }

    // How many punches this single row represents - 1 for a manual Scanner punch, or the
    // matching item quantity for an order-triggered punch, so a bulk order doesn't inflate
    // "visit count" analytics (count of PunchAdded rows) with N rows for one checkout.
    public int Quantity { get; set; } = 1;

    public string? CheckReference { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
