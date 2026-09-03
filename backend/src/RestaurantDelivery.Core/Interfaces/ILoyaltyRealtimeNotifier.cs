using RestaurantDelivery.Core.DTOs.Loyalty;

namespace RestaurantDelivery.Core.Interfaces;

// Plain abstraction (no SignalR types) so LoyaltyService/CampaignService - both in the
// Infrastructure class library, which has no ASP.NET Core framework reference - can push
// real-time updates without depending on IHubContext<T> directly. The concrete
// implementation (backed by IHubContext<LoyaltyHub>) lives in the Api project, where
// Microsoft.NET.Sdk.Web already provides that framework reference for free.
//
// Every method here must never throw - matching IWhatsAppNotificationService's contract,
// a failed push must never fail the underlying points/punch operation, which has already
// been committed to the database by the time either method is called.
public interface ILoyaltyRealtimeNotifier
{
    Task NotifyPointsUpdatedAsync(string customerId, PointsUpdatedPayload payload, CancellationToken ct = default);

    Task NotifyPunchUpdatedAsync(string customerId, PunchUpdatedPayload payload, CancellationToken ct = default);
}
