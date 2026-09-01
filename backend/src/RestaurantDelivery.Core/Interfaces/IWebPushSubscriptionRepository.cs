using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Core.Interfaces;

public interface IWebPushSubscriptionRepository : IGenericRepository<WebPushSubscription>
{
    Task<WebPushSubscription?> GetByEndpointAsync(string endpoint);
    Task<List<WebPushSubscription>> GetForCaptainsAsync();
    Task RemoveByEndpointAsync(string endpoint, string userId);
}
