namespace RestaurantDelivery.Core.DTOs.Settings;

public class RestaurantSettingsResponse
{
    public string? RestaurantName { get; set; }
    public string? LogoUrl { get; set; }
    public string PrimaryColor { get; set; } = string.Empty;
    public string AccentColor { get; set; } = string.Empty;
    public string HeaderColor { get; set; } = string.Empty;
    public string BodyColor { get; set; } = string.Empty;
    public string? BackgroundImageUrl { get; set; }
    public string? CenterLogoUrl { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? FooterAbout { get; set; }
    public string? FaviconUrl { get; set; }
    public string? TabTitle { get; set; }

    public decimal TaxPercentage { get; set; }
    public bool IsCashEnabled { get; set; }
    public bool IsVisaEnabled { get; set; }
    public string? VisaFawryUrl { get; set; }
    public bool IsInstapayEnabled { get; set; }
    public string? InstapayAccount { get; set; }
    public decimal BaseDeliveryFee { get; set; }
    public string? ManagerPhoneNumber { get; set; }
}
