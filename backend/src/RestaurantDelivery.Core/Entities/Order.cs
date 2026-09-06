using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.Entities;

public class Order
{
    public int Id { get; set; }

    // Null when placed as a guest checkout.
    public string? UserId { get; set; }
    public AppUser? User { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;

    // Grand total actually charged: (item subtotal - DiscountAmount) + TaxAmount + DeliveryFee.
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public string? Notes { get; set; }

    // Snapshot of the promo code applied at checkout (if any) - a plain string, not a
    // foreign key, so deleting/editing that PromoCode later never invalidates this
    // order's own historical record of what was actually applied.
    public string? PromoCodeText { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }

    // No backend-side delivery-fee configuration exists (CartService.DELIVERY_FEE is a
    // frontend constant) - snapshotted here purely so TotalAmount's breakdown always
    // adds up on a receipt, not because delivery pricing itself is now server-driven.
    public decimal DeliveryFee { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    // Only Visa starts Pending (see PaymentStatus's own doc comment) - there's no
    // dedicated "mark as paid" admin action yet, since one wasn't requested; an admin
    // can still see this value on the order to know a Visa payment needs confirming.
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Confirmed;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Set the first time this order's Delivered-transition finishes awarding loyalty
    // points/punches (see LoyaltyService.ProcessOrderDeliveredAsync) - checked before
    // re-processing so toggling the status away and back to Delivered can never award
    // points twice for the same order. Independent of the "was the previous status
    // already Delivered" check in OrderService.UpdateStatusAsync, which alone doesn't
    // survive a Delivered -> Cancelled -> Delivered round trip.
    public bool PointsAwarded { get; set; }

    // Set only for orders imported from an external POS (e.g. "Dgtera"). The pair
    // (ExternalSource, ExternalOrderId) is what DgteraSyncService matches on to decide
    // insert vs. update, so re-running a sync never creates duplicates. Both stay null
    // for orders placed normally through this app.
    public string? ExternalSource { get; set; }
    public string? ExternalOrderId { get; set; }

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
