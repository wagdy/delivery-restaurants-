namespace RestaurantDelivery.Core.Enums;

public enum OrderStatus
{
    Pending,
    Preparing,

    // Delivery leg.
    OutForDelivery,
    Delivered,

    // Collection leg, mirroring the two above for orders the customer picks up (see
    // Order.IsPickup). A pickup order that moved through OutForDelivery/Delivered told
    // the customer their food was "out for delivery" and then "delivered", neither of
    // which was true - these two say what actually happened instead.
    //
    // Both are also FULFILLED states, exactly like Delivered: they award loyalty points
    // and open up reviewing. See OrderStatuses.IsFulfilled, which is what every caller
    // should ask rather than comparing to Delivered directly.
    ReadyForCollection,
    Collected,

    Cancelled
}
