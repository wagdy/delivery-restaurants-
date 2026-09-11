using System.ComponentModel.DataAnnotations;
using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.DTOs.PromoCodes;

public class PromoCodeRequest : IValidatableObject
{
    [Required, MaxLength(50)]
    [RegularExpression(@"^[A-Za-z0-9_-]+$", ErrorMessage = "Code can only contain letters, numbers, hyphens, and underscores.")]
    public string CodeText { get; set; } = string.Empty;

    public PromoDiscountType DiscountType { get; set; }

    [Range(0.01, 100, ErrorMessage = "Discount value must be greater than 0 and no more than 100.")]
    public decimal DiscountValue { get; set; }

    // Category.Id values for SpecificCategory, MenuItem.Id values for SpecificItem -
    // ignored for Percentage/DeliveryDiscount (cleared server-side, see PromoCodeService).
    public List<int>? TargetIds { get; set; }

    [Required]
    public DateTime ExpiryDate { get; set; }

    public bool IsActive { get; set; } = true;

    // When true, PromoCodeService fires a WhatsApp broadcast to every active customer
    // announcing this code - see WhatsAppBroadcastBackgroundService. Applies on both
    // create and update (this DTO is shared by both), since re-announcing an edited code
    // is a legitimate, deliberate admin choice, not something to infer automatically.
    // Always defaults to false - a broadcast is opt-in per save, never implicit.
    public bool NotifyCustomersViaWhatsApp { get; set; }

    // A single DataAnnotation attribute can't express "TargetIds is required only for
    // these two DiscountType values" - hence IValidatableObject, mirroring TierRequest's
    // own MaxPoints > MinPoints cross-field check.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DiscountType is PromoDiscountType.SpecificCategory or PromoDiscountType.SpecificItem
            && (TargetIds is null || TargetIds.Count == 0))
        {
            var target = DiscountType == PromoDiscountType.SpecificCategory ? "category" : "menu item";
            yield return new ValidationResult($"Select at least one {target}.", new[] { nameof(TargetIds) });
        }

        if (ExpiryDate <= DateTime.UtcNow)
        {
            yield return new ValidationResult("Expiry date must be in the future.", new[] { nameof(ExpiryDate) });
        }
    }
}
