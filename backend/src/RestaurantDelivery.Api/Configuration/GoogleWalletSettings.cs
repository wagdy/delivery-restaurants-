namespace RestaurantDelivery.Api.Configuration;

public class GoogleWalletSettings
{
    public const string SectionName = "GoogleWallet";

    // The service account JSON key's raw content - set via the GoogleWallet__ServiceAccountKeyJson
    // Railway env var (no bind-mount secrets mechanism available here).
    public string? ServiceAccountKeyJson { get; set; }

    // Google Wallet issuer account ID from the Google Pay & Wallet Console.
    public string IssuerId { get; set; } = string.Empty;

    // Fully-qualified loyalty class ID (e.g. "3388000000022222222.otantik_loyalty_class"),
    // created ahead of time via the Google Pay & Wallet Console - this API only
    // creates/updates objects against it, it can't create the class itself.
    public string ClassId { get; set; } = string.Empty;

    public string IssuerName { get; set; } = "Otantik";

    // This API's own public origin (e.g. the api Railway domain), used to build the
    // LoyaltyClass HeroImage/ProgramLogo URLs - Google's servers fetch those directly.
    // Branding is skipped (not a hard failure) if this is left unset.
    public string? AssetBaseUrl { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ServiceAccountKeyJson) &&
        !string.IsNullOrWhiteSpace(IssuerId) &&
        !string.IsNullOrWhiteSpace(ClassId);
}
