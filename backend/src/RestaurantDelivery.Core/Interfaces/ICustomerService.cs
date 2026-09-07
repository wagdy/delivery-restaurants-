using RestaurantDelivery.Core.DTOs.Common;
using RestaurantDelivery.Core.DTOs.Customers;

namespace RestaurantDelivery.Core.Interfaces;

public interface ICustomerService
{
    Task<PagedResult<CustomerAnalyticsResponse>> GetAnalyticsPagedAsync(int page, int pageSize, string? search);

    // No paging - the export is a one-shot download of everything currently matching
    // (search is honored, same as the paged screen; there are no other filters yet).
    Task<Stream> ExportAnalyticsAsync(string? search);
}
