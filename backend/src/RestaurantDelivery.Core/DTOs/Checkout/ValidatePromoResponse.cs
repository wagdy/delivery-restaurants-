namespace RestaurantDelivery.Core.DTOs.Checkout;

public class ValidatePromoResponse
{
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal DeliveryDiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }

    // Fully computed, ready to display as the checkout page's "Final Total" - already
    // includes tax and nets out both discount amounts, so the frontend never has to
    // re-derive it from the individual fields above.
    public decimal Total { get; set; }

    public List<int> AffectedMenuItemIds { get; set; } = new();
    public string Description { get; set; } = string.Empty;
}
