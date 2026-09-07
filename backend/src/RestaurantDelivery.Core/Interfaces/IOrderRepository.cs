using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.Interfaces;

public interface IOrderRepository : IGenericRepository<Order>
{
    Task<Order?> GetByIdWithItemsAsync(int id);
    Task<Order?> GetByExternalIdAsync(string externalSource, string externalOrderId);
    Task<(List<Order> Orders, int TotalCount)> GetPagedWithItemsAsync(OrderStatus? status, int page, int pageSize);
    Task<List<Order>> GetByUserIdAsync(string userId);
    Task<List<MenuItem>> GetMenuItemsByIdsAsync(IEnumerable<int> ids);

    // For the admin "Create Order" POS screen's registered-customer phone search.
    Task<AppUser?> GetCustomerByPhoneAsync(string phoneNumber);
    Task<AppUser?> GetCustomerByIdAsync(string customerId);
}
