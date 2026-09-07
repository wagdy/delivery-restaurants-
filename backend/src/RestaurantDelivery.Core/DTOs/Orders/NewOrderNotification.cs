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

    // True when an admin entered this order themselves via the "Create Order" screen -
    // every connected admin dashboard still gets this push (so the order appears
    // instantly everywhere with no manual refresh), but none of them should ring the
    // alarm or show "needs acknowledgment" for something staff already knows about,
    // regardless of which cashier's tab actually typed it in.
    public bool IsStaffCreated { get; set; }
}
