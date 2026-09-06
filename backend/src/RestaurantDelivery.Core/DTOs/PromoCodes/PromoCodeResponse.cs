using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.DTOs.PromoCodes;

public class PromoCodeResponse
{
    public int Id { get; set; }
    public string CodeText { get; set; } = string.Empty;
    public PromoDiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public List<int>? TargetIds { get; set; }
    public DateTime ExpiryDate { get; set; }
    public bool IsActive { get; set; }

    // Convenience for the admin table - avoids every consumer re-deriving "expired"
    // from ExpiryDate and having to worry about UTC/local time itself.
    public bool IsExpired { get; set; }
}
