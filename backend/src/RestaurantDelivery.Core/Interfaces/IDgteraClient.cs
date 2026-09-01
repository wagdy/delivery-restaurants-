using RestaurantDelivery.Core.DTOs.Dgtera;

namespace RestaurantDelivery.Core.Interfaces;

public interface IDgteraClient
{
    // Returns orders from the connected Dgtera (Odoo) instance for DgteraSyncService to
    // upsert locally. What "recent" means (a fixed lookback window, or everything since
    // the last successful sync) is an implementation detail of the client.
    Task<List<DgteraOrderDto>> GetRecentOrdersAsync(CancellationToken cancellationToken = default);
}
