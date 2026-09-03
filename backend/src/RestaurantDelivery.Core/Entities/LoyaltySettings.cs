namespace RestaurantDelivery.Core.Entities;

// Singleton row (always the first one found) holding the global loyalty points ratios,
// editable from the Campaign Manager admin screen instead of being fixed in appsettings.
public class LoyaltySettings
{
    public int Id { get; set; }

    // Points awarded per 1 L.E. of check/order total. Default 0.1 preserves this app's
    // original fixed behavior of 1 point per 10 L.E. spent.
    public decimal PointsPerCurrencyUnit { get; set; } = 0.1m;

    // L.E. discount value granted per 100 points redeemed. Default 10 preserves this
    // app's original fixed behavior of 100 points being worth a 10 L.E. discount.
    public decimal RedemptionValuePer100Points { get; set; } = 10m;
}
