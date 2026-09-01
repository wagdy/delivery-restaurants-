namespace RestaurantDelivery.Core.DTOs.Settings;

public class RestaurantSettingsResponse
{
    public string RestaurantName { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string PrimaryColor { get; set; } = string.Empty;
    public string AccentColor { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? FooterAbout { get; set; }
}
