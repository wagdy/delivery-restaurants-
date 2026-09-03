using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.Common;

public static class MembershipTierCalculator
{
    public const int SilverThreshold = 500;
    public const int GoldThreshold = 1000;
    public const int VipThreshold = 2500;

    // Tiers only ever go up: a computed tier below the customer's current tier never downgrades them.
    public static MembershipTier CalculateTier(int totalLifetimePoints, MembershipTier currentTier)
    {
        var computedTier = totalLifetimePoints switch
        {
            >= VipThreshold => MembershipTier.VIP,
            >= GoldThreshold => MembershipTier.Gold,
            >= SilverThreshold => MembershipTier.Silver,
            _ => MembershipTier.Bronze
        };

        return computedTier > currentTier ? computedTier : currentTier;
    }
}
