using Microsoft.AspNetCore.SignalR;
using RestaurantDelivery.Api.Hubs;
using RestaurantDelivery.Core.DTOs.Orders;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Api.Services;

// Concrete IHubContext<OrderHub>-backed implementation of the Core-defined
// IOrderRealtimeNotifier abstraction - lives here (not in Infrastructure, where
// OrderService actually calls it) because Infrastructure is a plain Microsoft.NET.Sdk
// class library with no ASP.NET Core framework reference, while this Api project
// (Microsoft.NET.Sdk.Web) already has one for free. Mirrors LoyaltyRealtimeNotifier.
public class OrderRealtimeNotifier : IOrderRealtimeNotifier
{
    private readonly IHubContext<OrderHub> _hub;
    private readonly ILogger<OrderRealtimeNotifier> _logger;

    public OrderRealtimeNotifier(IHubContext<OrderHub> hub, ILogger<OrderRealtimeNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task NotifyNewOrderAsync(NewOrderNotification payload, CancellationToken ct = default)
    {
        try
        {
            // OrderHub's own [Authorize(Policy = "OrdersAccess")] already restricts who
            // can be connected to admins-with-Orders-module and captains, so Clients.All
            // here already means "every connected staff member", not literally everyone.
            await _hub.Clients.All.SendAsync("NewOrderReceived", payload, ct);
        }
        catch (Exception ex)
        {
            // Never let a push failure fail order creation - the order has already been
            // committed to the database by the time this is called.
            _logger.LogError(ex, "Failed to push NewOrderReceived for order {OrderId}.", payload.OrderId);
        }
    }
}
