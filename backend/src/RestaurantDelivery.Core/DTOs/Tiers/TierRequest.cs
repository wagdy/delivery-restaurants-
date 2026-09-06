using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Tiers;

public class TierRequest : IValidatableObject
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "Minimum points cannot be negative.")]
    public int MinPoints { get; set; }

    // Null means "no ceiling" (the top tier) - see LoyaltyTier.MaxPoints.
    [Range(0, int.MaxValue, ErrorMessage = "Maximum points cannot be negative.")]
    public int? MaxPoints { get; set; }

    // Cross-field validation (MaxPoints > MinPoints) can't be expressed with a single
    // [Range]/[Compare] attribute, since MaxPoints is optional and the comparison is
    // against another property, not a constant - IValidatableObject runs after the
    // per-property attributes above have already passed.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MaxPoints is int max && max <= MinPoints)
        {
            yield return new ValidationResult(
                "Maximum points must be greater than minimum points.",
                new[] { nameof(MaxPoints) });
        }
    }
}
