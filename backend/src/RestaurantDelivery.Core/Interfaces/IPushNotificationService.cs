using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Push;
using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Core.Interfaces;

public interface IPushNotificationService
{
    string GetVapidPublicKey();
    Task<ServiceResult<bool>> SubscribeAsync(string userId, PushSubscriptionRequest request);
    Task UnsubscribeAsync(string userId, string endpoint);

    // Fire-and-forget from the caller's perspective: failures are logged and swallowed
    // internally so a push delivery problem can never fail order creation itself.
    Task NotifyCaptainsOfNewOrderAsync(Order order);

    // System-level fallback for the admin/cashier audio alarm (see OrderRealtimeService on
    // the frontend): delivered via the browser's own push service, so it can wake the
    // cashier's device even if the PWA has been fully closed or the OS suspended its
    // SignalR connection. Same "never fail the caller" contract as the method above.
    Task NotifyCashiersOfNewOrderAsync(Order order);
}
