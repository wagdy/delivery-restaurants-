using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Google;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Walletobjects.v1.Data;
using Microsoft.Extensions.Options;
using RestaurantDelivery.Api.Configuration;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Infrastructure.Data;

namespace RestaurantDelivery.Api.Services.Loyalty;

// Uses the official Google.Apis.Walletobjects.v1 SDK to manage LoyaltyObjects against a
// pre-provisioned LoyaltyClass (created ahead of time via the Google Pay & Wallet Console).
public class GoogleWalletService : IGoogleWalletService
{
    private readonly GoogleWalletSettings _settings;
    private readonly IGoogleWalletClientProvider _clientProvider;
    private readonly ApplicationDbContext _context;
    private readonly Lock _brandingLock = new();
    private bool _brandingPatched;

    public GoogleWalletService(IOptions<GoogleWalletSettings> walletOptions, IGoogleWalletClientProvider clientProvider, ApplicationDbContext context)
    {
        _settings = walletOptions.Value;
        _clientProvider = clientProvider;
        _context = context;
    }

    public async Task<string> GenerateSaveLinkAsync(AppUser appUser, LoyaltyProfile profile, CancellationToken ct)
    {
        if (!_settings.IsConfigured)
        {
            throw new InvalidOperationException("Google Wallet is not configured: missing ServiceAccountKeyJson, IssuerId, or ClassId.");
        }

        try
        {
            await EnsureLoyaltyClassBrandingAsync(ct);

            var objectId = $"{_settings.IssuerId}.{appUser.Id}";
            await EnsureLoyaltyObjectAsync(appUser, profile, objectId, ct);

            return $"https://pay.google.com/gp/v/save/{BuildSaveToWalletJwt(objectId)}";
        }
        catch (TokenResponseException ex)
        {
            throw new InvalidOperationException($"Google Wallet authentication failed: {ex.Message}", ex);
        }
    }

    // Pushes branding onto the pre-provisioned LoyaltyClass - one Patch call per process
    // lifetime, re-applied on every restart so a manual Console edit can't silently drift.
    // Skipped, not failed, when AssetBaseUrl isn't set - branding is cosmetic.
    private async Task EnsureLoyaltyClassBrandingAsync(CancellationToken ct)
    {
        if (_brandingPatched || string.IsNullOrWhiteSpace(_settings.AssetBaseUrl))
        {
            return;
        }

        lock (_brandingLock)
        {
            if (_brandingPatched)
            {
                return;
            }
        }

        var patch = new LoyaltyClass
        {
            HeroImage = new Image { SourceUri = new ImageUri { Uri = $"{_settings.AssetBaseUrl}/loyalty-assets/hero.png" } },
            ProgramLogo = new Image { SourceUri = new ImageUri { Uri = $"{_settings.AssetBaseUrl}/loyalty-assets/logo.png" } }
        };

        try
        {
            await _clientProvider.WalletObjects.Loyaltyclass.Patch(patch, _settings.ClassId).ExecuteAsync(ct);
        }
        catch (GoogleApiException)
        {
            return;
        }

        lock (_brandingLock)
        {
            _brandingPatched = true;
        }
    }

    private async Task EnsureLoyaltyObjectAsync(AppUser appUser, LoyaltyProfile profile, string objectId, CancellationToken ct)
    {
        var loyaltyObject = new LoyaltyObject
        {
            Id = objectId,
            ClassId = _settings.ClassId,
            State = "ACTIVE",
            AccountId = appUser.Id,
            AccountName = appUser.FullName,
            Barcode = new Barcode { Type = "QR_CODE", Value = appUser.Id },
            LoyaltyPoints = new LoyaltyPoints
            {
                Label = "Points",
                Balance = new LoyaltyPointsBalance { String__ = profile.CurrentPoints.ToString() }
            }
        };

        try
        {
            await _clientProvider.WalletObjects.Loyaltyobject.Insert(loyaltyObject).ExecuteAsync(ct);
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Conflict)
        {
            await _clientProvider.WalletObjects.Loyaltyobject.Update(loyaltyObject, objectId).ExecuteAsync(ct);
        }

        if (!profile.HasGoogleWalletObject)
        {
            profile.HasGoogleWalletObject = true;
            await _context.SaveChangesAsync(ct);
        }
    }

    private string BuildSaveToWalletJwt(string objectId)
    {
        var now = DateTimeOffset.UtcNow;
        var header = new JsonObject { ["alg"] = "RS256", ["typ"] = "JWT" };
        var payload = new JsonObject
        {
            ["iss"] = _clientProvider.ClientEmail,
            ["aud"] = "google",
            ["typ"] = "savetowallet",
            ["iat"] = now.ToUnixTimeSeconds(),
            ["payload"] = new JsonObject
            {
                ["loyaltyObjects"] = new JsonArray(new JsonObject { ["id"] = objectId })
            }
        };

        var headerB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(header.ToJsonString()));
        var payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payload.ToJsonString()));
        var signingInput = $"{headerB64}.{payloadB64}";

        return $"{signingInput}.{_clientProvider.SignRs256(signingInput)}";
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
