using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.Interfaces;

public interface IOrderRepository : IGenericRepository<Order>
{
    // Explicit transaction control, needed by CreateAsync: an order that redeems loyalty
    // points must commit the order row and the points deduction together, or a failure
    // between them spends a customer's points on an order that does not exist. Kept as
    // three plain methods so no EF type leaks into Core.
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);

    Task<Order?> GetByIdWithItemsAsync(int id);
    Task<Order?> GetByExternalIdAsync(string externalSource, string externalOrderId);
    Task<(List<Order> Orders, int TotalCount)> GetPagedWithItemsAsync(OrderStatus? status, int page, int pageSize);
    Task<List<Order>> GetByUserIdAsync(string userId);
    Task<List<MenuItem>> GetMenuItemsByIdsAsync(IEnumerable<int> ids);

    // For the admin "Create Order" POS screen's registered-customer phone search.
    Task<AppUser?> GetCustomerByPhoneAsync(string phoneNumber);
    Task<AppUser?> GetCustomerByIdAsync(string customerId);
}
