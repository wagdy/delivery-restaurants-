namespace RestaurantDelivery.Api.Configuration;

public class AppleWalletSettings
{
    public const string SectionName = "AppleWallet";

    public string TeamIdentifier { get; set; } = string.Empty;

    public string PassTypeIdentifier { get; set; } = string.Empty;

    public string OrganizationName { get; set; } = string.Empty;

    // Base URL the Wallet app calls back to for pass updates, e.g.
    // "https://api-production-xxxx.up.railway.app/api/loyalty/wallet/apple" - iOS's own
    // Wallet daemon appends "/v1/devices/...", "/v1/passes/...", "/v1/log" itself, so this
    // must NOT include a trailing "/v1".
    public string WebServiceUrl { get; set; } = string.Empty;

    // Railway has no bind-mount secrets mechanism (unlike a VPS), only env vars - these
    // carry the Pass Type ID certificate (.p12) and Apple's WWDR intermediate certificate
    // (.pem/.cer) as base64, decoded straight to bytes and loaded via
    // X509CertificateLoader.LoadPkcs12(byte[], ...)/LoadCertificate(byte[]) - no temp files.
    public string PassCertificateBase64 { get; set; } = string.Empty;

    public string PassCertificatePassword { get; set; } = string.Empty;

    public string WwdrCertificateBase64 { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(PassCertificateBase64) &&
        !string.IsNullOrWhiteSpace(WwdrCertificateBase64) &&
        !string.IsNullOrWhiteSpace(TeamIdentifier) &&
        !string.IsNullOrWhiteSpace(PassTypeIdentifier) &&
        !string.IsNullOrWhiteSpace(WebServiceUrl) &&
        RequiredAssetsExist();

    // The pass's icon/logo/strip artwork isn't shipped with this migration (see the
    // RestaurantDelivery.Api.csproj comment) - treated the same as a missing certificate:
    // the feature stays off until real branding is dropped into Assets/ApplePass.
    private static bool RequiredAssetsExist()
    {
        var assetsFolder = Path.Combine(AppContext.BaseDirectory, "Assets", "ApplePass");
        string[] required =
        [
            "icon.png", "icon@2x.png", "icon@3x.png",
            "logo.png", "logo@2x.png", "logo@3x.png",
            "strip.png", "strip@2x.png", "strip@3x.png"
        ];

        return required.All(name => File.Exists(Path.Combine(assetsFolder, name)));
    }
}
