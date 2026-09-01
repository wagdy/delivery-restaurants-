using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Settings;

public class UpdateRestaurantSettingsRequest
{
    [Required, MaxLength(200)]
    public string RestaurantName { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string? LogoUrl { get; set; }

    [Required, RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Must be a hex color like #3f51b5.")]
    public string PrimaryColor { get; set; } = "#3f51b5";

    [Required, RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Must be a hex color like #ff4081.")]
    public string AccentColor { get; set; } = "#ff4081";

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(30)]
    public string? Phone { get; set; }

    [MaxLength(256), EmailAddress]
    public string? Email { get; set; }

    [MaxLength(1000)]
    public string? FooterAbout { get; set; }
}
