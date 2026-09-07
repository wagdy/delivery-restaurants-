using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.DTOs.Orders;

public class OrderResponse
{
    public int Id { get; set; }
    public string? UserId { get; set; }

    // "Guest" or "Registered" - a plain derived string (UserId is null ? "Guest" :
    // "Registered", see OrderService.MapResponse) rather than a bool, so the cashier
    // dashboard's badge can bind to it directly with no client-side branching, and so a
    // future third customer status wouldn't require an API-breaking type change.
    public string CustomerStatus { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<OrderItemResponse> Items { get; set; } = new();

    public string? PromoCodeText { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DeliveryFee { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus PaymentStatus { get; set; }

    public bool IsAcknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }

    // Convenience for the order-confirmation/summary UI - the pre-tax, pre-discount sum
    // of line items, derived rather than stored (TotalAmount is the only money value
    // that's actually persisted as "the truth"; this and the fields above are its
    // breakdown for display).
    public decimal Subtotal => Items.Sum(i => i.LineTotal);
}
