using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Core.Interfaces;

public interface IWebPushSubscriptionRepository : IGenericRepository<WebPushSubscription>
{
    Task<WebPushSubscription?> GetByEndpointAsync(string endpoint);
    Task<List<WebPushSubscription>> GetForCaptainsAsync();

    // "Cashier" = an Admin account that can actually see the Orders tab: either a
    // superuser (no CustomRoleId, full access - see AuthService.ResolveAdminModuleNamesAsync)
    // or one whose assigned Role has the Orders module flag. An Admin restricted to e.g.
    // Menu Items only has no reason to be paged for a new order.
    Task<List<WebPushSubscription>> GetForCashiersAsync();
    Task RemoveByEndpointAsync(string endpoint, string userId);
}
