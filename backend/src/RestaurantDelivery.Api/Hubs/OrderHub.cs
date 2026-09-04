using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RestaurantDelivery.Api.Hubs;

// Staff-only: "OrdersAccess" is the exact same policy the REST GET /api/orders list
// endpoint already uses (an Admin with the Orders module, or any CaptainOrder). Since
// only the right people can even establish a connection here in the first place,
// broadcasting to Clients.All (see OrderRealtimeNotifier) already reaches exactly
// "every connected admin/cashier" with no separate SignalR group bookkeeping needed.
[Authorize(Policy = "OrdersAccess")]
public class OrderHub : Hub
{
}
