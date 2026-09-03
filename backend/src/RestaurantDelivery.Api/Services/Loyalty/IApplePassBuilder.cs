using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Api.Services.Loyalty;

public interface IApplePassBuilder
{
    // Throws InvalidOperationException if Apple Wallet isn't configured - callers should
    // check AppleWalletSettings.IsConfigured first and return a 503 rather than call this.
    byte[] Build(AppUser appUser, LoyaltyProfile profile, string qrPayload);
}
