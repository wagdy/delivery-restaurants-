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
}
