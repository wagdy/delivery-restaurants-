using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace RestaurantDelivery.Api.Services.Loyalty;

// Derives the token via HMAC over the AppUser id rather than storing one per pass -
// stable across pass regenerations and needs no extra table. Reuses the same Jwt:Key
// already configured for customer login JWTs.
public class WalletAuthTokenService : IWalletAuthTokenService
{
    private readonly byte[] _key;

    public WalletAuthTokenService(IConfiguration configuration)
    {
        var jwtKey = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        _key = Encoding.UTF8.GetBytes(jwtKey);
    }

    public string GetAuthenticationToken(string appUserId)
    {
        var input = Encoding.UTF8.GetBytes($"wallet-auth:{appUserId}");
        var hash = HMACSHA256.HashData(_key, input);
        return Convert.ToHexString(hash);
    }

    public bool Validate(string appUserId, string? presentedToken)
    {
        if (string.IsNullOrEmpty(presentedToken))
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(GetAuthenticationToken(appUserId));
        var presented = Encoding.UTF8.GetBytes(presentedToken);

        return expected.Length == presented.Length && CryptographicOperations.FixedTimeEquals(expected, presented);
    }
}
