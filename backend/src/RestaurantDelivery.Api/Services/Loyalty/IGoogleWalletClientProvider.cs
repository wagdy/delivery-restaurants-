using Google.Apis.Walletobjects.v1;

namespace RestaurantDelivery.Api.Services.Loyalty;

public interface IGoogleWalletClientProvider
{
    WalletobjectsService WalletObjects { get; }

    string ClientEmail { get; }

    string SignRs256(string signingInput);
}
