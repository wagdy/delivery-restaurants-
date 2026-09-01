using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Enums;
using RestaurantDelivery.Core.Interfaces;
using RestaurantDelivery.Infrastructure.Data;

namespace RestaurantDelivery.Infrastructure.Repositories;

public class WebPushSubscriptionRepository : GenericRepository<WebPushSubscription>, IWebPushSubscriptionRepository
{
    public WebPushSubscriptionRepository(ApplicationDbContext context) : base(context)
    {
    }

    public Task<WebPushSubscription?> GetByEndpointAsync(string endpoint) =>
        DbSet.FirstOrDefaultAsync(s => s.Endpoint == endpoint);

    public Task<List<WebPushSubscription>> GetForCaptainsAsync() =>
        DbSet.Where(s => s.User.Role == UserRole.CaptainOrder).ToListAsync();

    public async Task RemoveByEndpointAsync(string endpoint, string userId)
    {
        var subscription = await DbSet.FirstOrDefaultAsync(s => s.Endpoint == endpoint && s.UserId == userId);
        if (subscription is not null)
        {
            DbSet.Remove(subscription);
        }
    }
}
