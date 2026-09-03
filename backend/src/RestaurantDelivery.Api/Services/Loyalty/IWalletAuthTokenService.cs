namespace RestaurantDelivery.Api.Services.Loyalty;

// The Apple PassKit Web Service's own per-pass auth scheme (Authorization: "ApplePass
// <token>", presented by the Wallet app itself when it calls back to check for pass
// updates) - unrelated to customer login/JWT auth.
public interface IWalletAuthTokenService
{
    string GetAuthenticationToken(string appUserId);

    bool Validate(string appUserId, string? presentedToken);
}
