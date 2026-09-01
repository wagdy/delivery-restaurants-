using RestaurantDelivery.Core.DTOs.Dgtera;

namespace RestaurantDelivery.Core.Interfaces;

public interface IDgteraSyncService
{
    Task<SyncOrdersResult> SyncOrdersAsync(CancellationToken cancellationToken = default);
}
