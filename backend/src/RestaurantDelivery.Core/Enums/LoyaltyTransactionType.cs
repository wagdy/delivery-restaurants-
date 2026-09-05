namespace RestaurantDelivery.Core.Enums;

public enum LoyaltyTransactionType
{
    Earned = 0,
    Redeemed = 1,
    AdminAdjustment = 2,

    // Auto-awarded when an order transitions to Delivered - see LoyaltyService.ProcessOrderDeliveredAsync.
    OrderEarned = 3,

    // Auto-awarded once, immediately after a new customer registers - see
    // LoyaltyService.AwardWelcomeBonusAsync and AuthService.RegisterAsync.
    WelcomeBonus = 4
}
