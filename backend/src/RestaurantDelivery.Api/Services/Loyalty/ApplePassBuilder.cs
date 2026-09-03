using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using Passbook.Generator;
using Passbook.Generator.Fields;
using RestaurantDelivery.Api.Configuration;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Api.Services.Loyalty;

public class ApplePassBuilder : IApplePassBuilder
{
    private static readonly Dictionary<MembershipTier, (string Background, string Foreground, string Label)> TierColors = new()
    {
        [MembershipTier.Bronze] = ("rgb(107,74,47)", "rgb(255,255,255)", "rgb(230,214,200)"),
        [MembershipTier.Silver] = ("rgb(117,117,117)", "rgb(255,255,255)", "rgb(230,230,230)"),
        [MembershipTier.Gold] = ("rgb(150,120,20)", "rgb(255,255,255)", "rgb(250,235,190)"),
        [MembershipTier.VIP] = ("rgb(28,18,16)", "rgb(212,175,55)", "rgb(212,175,55)")
    };

    private readonly AppleWalletSettings _settings;
    private readonly IWalletAuthTokenService _authTokenService;

    public ApplePassBuilder(IOptions<AppleWalletSettings> walletOptions, IWalletAuthTokenService authTokenService)
    {
        _settings = walletOptions.Value;
        _authTokenService = authTokenService;
    }

    public byte[] Build(AppUser appUser, LoyaltyProfile profile, string qrPayload)
    {
        if (!_settings.IsConfigured)
        {
            throw new InvalidOperationException(
                "Apple Wallet is not configured: missing certificates, identifiers, or pass artwork.");
        }

        var (background, foreground, label) = TierColors[profile.MembershipTier];

        var request = new PassGeneratorRequest
        {
            PassTypeIdentifier = _settings.PassTypeIdentifier,
            TeamIdentifier = _settings.TeamIdentifier,
            OrganizationName = _settings.OrganizationName,
            Description = $"{_settings.OrganizationName} Loyalty Card",
            SerialNumber = appUser.Id,
            WebServiceUrl = _settings.WebServiceUrl,
            AuthenticationToken = _authTokenService.GetAuthenticationToken(appUser.Id),
            BackgroundColor = background,
            ForegroundColor = foreground,
            LabelColor = label,
            Style = PassStyle.StoreCard
        };

        request.Images.Add(PassbookImage.Icon, LoadAsset("icon.png"));
        request.Images.Add(PassbookImage.Icon2X, LoadAsset("icon@2x.png"));
        request.Images.Add(PassbookImage.Icon3X, LoadAsset("icon@3x.png"));
        request.Images.Add(PassbookImage.Logo, LoadAsset("logo.png"));
        request.Images.Add(PassbookImage.Logo2X, LoadAsset("logo@2x.png"));
        request.Images.Add(PassbookImage.Logo3X, LoadAsset("logo@3x.png"));
        request.Images.Add(PassbookImage.Strip, LoadAsset("strip.png"));
        request.Images.Add(PassbookImage.Strip2X, LoadAsset("strip@2x.png"));
        request.Images.Add(PassbookImage.Strip3X, LoadAsset("strip@3x.png"));

        request.HeaderFields.Add(new NumberField("points", "Points", profile.CurrentPoints, FieldNumberStyle.PKNumberStyleDecimal));
        request.PrimaryFields.Add(new StandardField("name", "Member", appUser.FullName));
        request.SecondaryFields.Add(new StandardField("tier", "Tier", profile.MembershipTier.ToString()));
        request.SecondaryFields.Add(new StandardField("referral", "Referral Code", profile.ReferralCode));
        request.AuxiliaryFields.Add(new NumberField("lifetime", "Lifetime Points", profile.TotalLifetimePoints, FieldNumberStyle.PKNumberStyleDecimal));

        request.BackFields.Add(new StandardField(
            "about", "About", $"Present this card at {_settings.OrganizationName} to earn and redeem points."));

        request.Barcodes.Add(new Barcode(BarcodeType.PKBarcodeFormatQR, qrPayload, "iso-8859-1", profile.ReferralCode));

        var passCertificateBytes = Convert.FromBase64String(_settings.PassCertificateBase64);
        var wwdrCertificateBytes = Convert.FromBase64String(_settings.WwdrCertificateBase64);

        request.PassbookCertificate = X509CertificateLoader.LoadPkcs12(
            passCertificateBytes, _settings.PassCertificatePassword, X509KeyStorageFlags.Exportable);
        request.AppleWWDRCACertificate = X509CertificateLoader.LoadCertificate(wwdrCertificateBytes);

        var generator = new PassGenerator();
        return generator.Generate(request);
    }

    private static byte[] LoadAsset(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "ApplePass", fileName);
        return File.ReadAllBytes(path);
    }
}
