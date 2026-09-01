using RestaurantDelivery.Core.DTOs.Orders;

namespace RestaurantDelivery.Core.Interfaces;

public interface IBulkOrderImportService
{
    // Caller owns and disposes the returned stream.
    Stream GenerateTemplate();

    Task<BulkOrderImportResult> ImportOrdersAsync(Stream fileStream);
}
