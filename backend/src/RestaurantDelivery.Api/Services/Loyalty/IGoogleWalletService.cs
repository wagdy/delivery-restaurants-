using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Api.Services.Loyalty;

public interface IGoogleWalletService
{
    // Creates/updates the customer's LoyaltyObject against the configured ClassId, then
    // returns a signed "https://pay.google.com/gp/v/save/<jwt>" link referencing it.
    // Throws InvalidOperationException if Google Wallet isn't configured.
    Task<string> GenerateSaveLinkAsync(AppUser appUser, LoyaltyProfile profile, CancellationToken ct);
}
