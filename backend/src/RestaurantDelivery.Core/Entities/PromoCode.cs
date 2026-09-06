using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.Entities;

public class PromoCode
{
    public int Id { get; set; }

    // Stored/compared uppercase-trimmed (see PromoCodeService) so "SAVE10" and "save10"
    // are the same code - customers shouldn't lose a discount to shift-key luck.
    public string CodeText { get; set; } = string.Empty;

    public PromoDiscountType DiscountType { get; set; }

    // Always a percentage 0-100, meaning depends on DiscountType - see that enum's own
    // doc comment and PromoDiscountCalculator, the single place this is interpreted.
    public decimal DiscountValue { get; set; }

    // JSON-encoded int array (e.g. "[1,2,3]") - Category.Id values for
    // SpecificCategory, MenuItem.Id values for SpecificItem, null/unused for
    // Percentage and DeliveryDiscount. Kept as a single flexible string column rather
    // than two separate FK collections since exactly one interpretation ever applies
    // for a given row, matching the shape the user's own spec asked for.
    public string? TargetIds { get; set; }

    public DateTime ExpiryDate { get; set; }

    public bool IsActive { get; set; } = true;
}
