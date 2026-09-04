using RestaurantDelivery.Core.DTOs.Orders;

namespace RestaurantDelivery.Core.Interfaces;

// Plain abstraction (no SignalR types) so OrderService - in the Infrastructure class
// library, which has no ASP.NET Core framework reference - can push real-time updates
// without depending on IHubContext<T> directly. Mirrors ILoyaltyRealtimeNotifier's
// Core/Api split exactly; see that interface for the full explanation. The concrete
// implementation (backed by IHubContext<OrderHub>) lives in the Api project.
//
// Must never throw - a failed push must never fail order creation, which has already
// been committed to the database by the time this is called.
public interface IOrderRealtimeNotifier
{
    Task NotifyNewOrderAsync(NewOrderNotification payload, CancellationToken ct = default);
}
