using System.Text;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Walletobjects.v1;
using Microsoft.Extensions.Options;
using RestaurantDelivery.Api.Configuration;

namespace RestaurantDelivery.Api.Services.Loyalty;

public class GoogleWalletClientProvider : IGoogleWalletClientProvider, IDisposable
{
    private readonly GoogleWalletSettings _settings;
    private readonly Lock _lock = new();
    private WalletobjectsService? _walletObjects;
    private ServiceAccountCredential? _credential;

    public GoogleWalletClientProvider(IOptions<GoogleWalletSettings> walletOptions)
    {
        _settings = walletOptions.Value;
    }

    public WalletobjectsService WalletObjects
    {
        get
        {
            EnsureLoaded();
            return _walletObjects!;
        }
    }

    public string ClientEmail
    {
        get
        {
            EnsureLoaded();
            return _credential!.Id;
        }
    }

    public string SignRs256(string signingInput)
    {
        EnsureLoaded();

        // ServiceAccountCredential.CreateSignature returns a standard (not URL-safe) base64
        // string - JWTs need base64url, so re-encode before use.
        var standardBase64 = _credential!.CreateSignature(Encoding.UTF8.GetBytes(signingInput));
        return standardBase64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private void EnsureLoaded()
    {
        lock (_lock)
        {
            if (_walletObjects is not null)
            {
                return;
            }

            var serviceAccountCredential = CredentialFactory.FromStream<ServiceAccountCredential>(
                new MemoryStream(Encoding.UTF8.GetBytes(_settings.ServiceAccountKeyJson!)));

            var googleCredential = serviceAccountCredential
                .ToGoogleCredential()
                .CreateScoped(WalletobjectsService.Scope.WalletObjectIssuer);

            _credential = (ServiceAccountCredential)googleCredential.UnderlyingCredential;
            _walletObjects = new WalletobjectsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = googleCredential,
                ApplicationName = _settings.IssuerName
            });
        }
    }

    public void Dispose() => _walletObjects?.Dispose();
}
