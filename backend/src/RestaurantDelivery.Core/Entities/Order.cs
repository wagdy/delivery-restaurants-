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

    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Set only for orders imported from an external POS (e.g. "Dgtera"). The pair
    // (ExternalSource, ExternalOrderId) is what DgteraSyncService matches on to decide
    // insert vs. update, so re-running a sync never creates duplicates. Both stay null
    // for orders placed normally through this app.
    public string? ExternalSource { get; set; }
    public string? ExternalOrderId { get; set; }

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
