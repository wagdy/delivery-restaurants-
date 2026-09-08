using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Settings;

public class UpdateRestaurantSettingsRequest : IValidatableObject
{
    [MaxLength(200)]
    public string? RestaurantName { get; set; }

    [MaxLength(2048)]
    public string? LogoUrl { get; set; }

    [Required, RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Must be a hex color like #3f51b5.")]
    public string PrimaryColor { get; set; } = "#3f51b5";

    [Required, RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Must be a hex color like #ff4081.")]
    public string AccentColor { get; set; } = "#ff4081";

    [Required, RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Must be a hex color like #3f51b5.")]
    public string HeaderColor { get; set; } = "#3f51b5";

    [Required, RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Must be a hex color like #fafafa.")]
    public string BodyColor { get; set; } = "#fafafa";

    [MaxLength(2048)]
    public string? BackgroundImageUrl { get; set; }

    [MaxLength(2048)]
    public string? CenterLogoUrl { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(30)]
    public string? Phone { get; set; }

    [MaxLength(256), EmailAddress]
    public string? Email { get; set; }

    [MaxLength(1000)]
    public string? FooterAbout { get; set; }

    [MaxLength(2048)]
    public string? FaviconUrl { get; set; }

    [MaxLength(100)]
    public string? TabTitle { get; set; }

    [Range(0, 100, ErrorMessage = "Tax percentage must be between 0 and 100.")]
    public decimal TaxPercentage { get; set; }

    public bool IsCashEnabled { get; set; } = true;

    public bool IsVisaEnabled { get; set; }

    [MaxLength(2048)]
    public string? VisaFawryUrl { get; set; }

    public bool IsInstapayEnabled { get; set; }

    [MaxLength(200)]
    public string? InstapayAccount { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Delivery fee cannot be negative.")]
    public decimal BaseDeliveryFee { get; set; }

    [MaxLength(30)]
    public string? ManagerWhatsApp1 { get; set; }

    [MaxLength(30)]
    public string? ManagerWhatsApp2 { get; set; }

    [MaxLength(30)]
    public string? ManagerWhatsApp3 { get; set; }

    // A single DataAnnotation attribute can't make VisaFawryUrl/InstapayAccount
    // conditionally required based on their matching toggle, hence IValidatableObject.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IsVisaEnabled && string.IsNullOrWhiteSpace(VisaFawryUrl))
        {
            yield return new ValidationResult(
                "A Fawry link is required while Visa payment is enabled.",
                new[] { nameof(VisaFawryUrl) });
        }

        if (IsInstapayEnabled && string.IsNullOrWhiteSpace(InstapayAccount))
        {
            yield return new ValidationResult(
                "An Instapay account is required while Instapay payment is enabled.",
                new[] { nameof(InstapayAccount) });
        }
    }
}
