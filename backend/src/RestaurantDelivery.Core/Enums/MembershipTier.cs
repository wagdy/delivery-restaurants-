namespace RestaurantDelivery.Core.Enums;

// Ordinal order matters: values must ascend with tier rank for upgrade comparisons in
// MembershipTierCalculator.
public enum MembershipTier
{
    Bronze = 0,
    Silver = 1,
    Gold = 2,
    VIP = 3
}
