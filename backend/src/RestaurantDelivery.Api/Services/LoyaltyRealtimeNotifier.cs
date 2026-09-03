using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using RestaurantDelivery.Api.Hubs;
using RestaurantDelivery.Core.DTOs.Loyalty;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Api.Services;

// Concrete IHubContext<LoyaltyHub>-backed implementation of the Core-defined
// ILoyaltyRealtimeNotifier abstraction - lives here (not in Infrastructure, where
// LoyaltyService/CampaignService actually call it) because Infrastructure is a plain
// Microsoft.NET.Sdk class library with no ASP.NET Core framework reference, while this
// Api project (Microsoft.NET.Sdk.Web) already has one for free.
public class LoyaltyRealtimeNotifier : ILoyaltyRealtimeNotifier
{
    private readonly IHubContext<LoyaltyHub> _hub;
    private readonly ILogger<LoyaltyRealtimeNotifier> _logger;

    public LoyaltyRealtimeNotifier(IHubContext<LoyaltyHub> hub, ILogger<LoyaltyRealtimeNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public Task NotifyPointsUpdatedAsync(string customerId, PointsUpdatedPayload payload, CancellationToken ct = default) =>
        SendAsync(customerId, "PointsUpdated", payload, ct);

    public Task NotifyPunchUpdatedAsync(string customerId, PunchUpdatedPayload payload, CancellationToken ct = default) =>
        SendAsync(customerId, "PunchUpdated", payload, ct);

    private async Task SendAsync(string customerId, string eventName, object payload, CancellationToken ct)
    {
        try
        {
            await _hub.Clients.User(customerId).SendAsync(eventName, payload, ct);
        }
        catch (Exception ex)
        {
            // Never let a push failure fail the caller - the underlying points/punch
            // change has already been committed to the database by this point regardless.
            _logger.LogError(ex, "Failed to push {Event} to customer {CustomerId}.", eventName, customerId);
        }
    }
}
