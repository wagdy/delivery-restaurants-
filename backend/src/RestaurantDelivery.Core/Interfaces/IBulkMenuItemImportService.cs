using RestaurantDelivery.Core.DTOs.MenuItems;

namespace RestaurantDelivery.Core.Interfaces;

public interface IBulkMenuItemImportService
{
    // Caller owns and disposes the returned stream.
    Stream GenerateTemplate();

    Task<BulkMenuItemImportResult> ImportMenuItemsAsync(Stream fileStream);
}
