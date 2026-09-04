namespace RestaurantDelivery.Core.DTOs.Orders;

// Lightweight real-time push payload for OrderHub's "NewOrderReceived" event - deliberately
// not the full OrderResponse shape (items/add-ons/etc.), matching PointsUpdatedPayload and
// PunchUpdatedPayload's convention of a small summary DTO for the socket, not the full
// entity. The admin dashboard fetches the complete order by OrderId once notified, so this
// only needs enough to drive the toast notification.
public class NewOrderNotification
{
    public int OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
