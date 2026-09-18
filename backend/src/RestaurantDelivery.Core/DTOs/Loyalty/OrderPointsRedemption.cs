namespace RestaurantDelivery.Core.DTOs.Loyalty;

// What a checkout redemption actually did, as decided by the server.
//
// PointsSpent can be LESS than the customer asked for: points are capped at what the
// order can absorb, so redeeming 1000 points against a 40 L.E. order spends only what
// 40 L.E. is worth and leaves the rest in the account. Burning the remainder for nothing
// would be the customer's money quietly disappearing.
public sealed class OrderPointsRedemption
{
    public int PointsSpent { get; init; }
    public decimal DiscountAmount { get; init; }
    public int RemainingBalance { get; init; }

    public static OrderPointsRedemption None => new();
}
