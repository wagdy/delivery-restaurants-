using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Checkout;

public class PromoCartItemRequest
{
    [Required]
    public int MenuItemId { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; }

    public List<int> AddOnIds { get; set; } = new();
}

public class ValidatePromoRequest
{
    [Required, MaxLength(50)]
    public string CodeText { get; set; } = string.Empty;

    [MinLength(1)]
    public List<PromoCartItemRequest> Items { get; set; } = new();

    // The frontend's own known delivery fee (see CartService.deliveryFee) - there's no
    // backend-side delivery fee configuration today, so this is passed in rather than
    // duplicated as a second source of truth. Needed here specifically so a
    // DeliveryDiscount promo has something to discount.
    [Range(0, double.MaxValue)]
    public decimal DeliveryFee { get; set; }
}
