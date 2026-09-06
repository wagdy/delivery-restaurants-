namespace RestaurantDelivery.Core.Entities;

// Admin-configurable replacement for the old hardcoded Bronze/Silver/Gold/VIP enum - a
// customer's current tier is resolved dynamically by finding the row whose
// [MinPoints, MaxPoints] range contains their TotalLifetimePoints (see
// LoyaltyService.ResolveTierNameAsync), not stored as a fixed enum anywhere.
public class LoyaltyTier
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MinPoints { get; set; }

    // Null means "no ceiling" - the top tier (e.g. VIP) never has an upper bound.
    public int? MaxPoints { get; set; }
}
