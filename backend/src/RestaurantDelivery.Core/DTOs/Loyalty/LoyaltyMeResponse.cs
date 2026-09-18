namespace RestaurantDelivery.Core.DTOs.Loyalty;

public class LoyaltyMeResponse
{
    public string AppUserId { get; set; } = string.Empty;
    public int CurrentPoints { get; set; }
    public int TotalLifetimePoints { get; set; }
    public string MembershipTier { get; set; } = string.Empty;
    public string ReferralCode { get; set; } = string.Empty;

    // The live conversion rate, sent so the checkout can show the customer what their
    // points are worth BEFORE they commit. The server recalculates from its own settings
    // when the order is placed - this is for display, never the basis of a charge.
    public decimal RedemptionValuePer100Points { get; set; }

    // Reflect each wallet's server-side IsConfigured state, so the frontend can hide/
    // disable the matching "Add to Wallet" button rather than surface a 503 on click.
    public bool AppleWalletAvailable { get; set; }
    public bool GoogleWalletAvailable { get; set; }
}
