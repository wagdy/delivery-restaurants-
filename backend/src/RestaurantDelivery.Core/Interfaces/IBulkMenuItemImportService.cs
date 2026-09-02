using RestaurantDelivery.Core.DTOs.MenuItems;

namespace RestaurantDelivery.Core.Interfaces;

public interface IBulkMenuItemImportService
{
    // Caller owns and disposes the returned stream. Async because it queries the
    // Categories table to populate the template's dropdown list.
    Task<Stream> GenerateTemplate();

    Task<BulkMenuItemImportResult> ImportMenuItemsAsync(Stream fileStream);
}
