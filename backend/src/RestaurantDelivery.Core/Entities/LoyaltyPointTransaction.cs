using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.Entities;

public class LoyaltyPointTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string CustomerId { get; set; } = string.Empty;

    public AppUser Customer { get; set; } = null!;

    // The staff member (Admin with Module.Customers) who awarded/redeemed the points.
    public string? AdminId { get; set; }

    public AppUser? Admin { get; set; }

    // Positive for earned, negative for redeemed/admin adjustments that deduct.
    public int PointsTransacted { get; set; }

    // The physical bill amount in L.E. that produced this transaction - only set for Earned.
    public decimal? CheckAmount { get; set; }

    public LoyaltyTransactionType TransactionType { get; set; }

    public string? CheckReference { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Set only for TransactionType.OrderEarned - the order whose delivery triggered this
    // transaction. A unique index on this column (see LoyaltyPointTransactionConfiguration)
    // guards against double-awarding if the same order is marked Delivered twice.
    public int? OrderId { get; set; }

    public Order? Order { get; set; }
}
