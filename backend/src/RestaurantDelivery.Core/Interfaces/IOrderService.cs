using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Common;
using RestaurantDelivery.Core.DTOs.Orders;
using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.Interfaces;

public interface IOrderService
{
    // isStaffCreated marks orders keyed in by an admin (Create Order screen) so the
    // real-time notification tells every connected dashboard not to ring its alarm for
    // something staff already knows about - see NewOrderNotification.IsStaffCreated.
    Task<ServiceResult<OrderResponse>> CreateAsync(CreateOrderRequest request, string? userId, bool isStaffCreated = false);
    Task<ServiceResult<PagedResult<OrderResponse>>> GetAllAsync(OrderStatus? status, int page, int pageSize);
    Task<ServiceResult<OrderResponse>> GetByIdAsync(int id);
    Task<ServiceResult<List<OrderResponse>>> GetMyOrdersAsync(string userId);
    Task<ServiceResult<OrderResponse>> UpdateAsync(int id, UpdateOrderRequest request);
    Task<ServiceResult<OrderResponse>> UpdateStatusAsync(int id, OrderStatus status);

    // Dismisses the cashier dashboard's new-order alarm for this order specifically -
    // see Order.IsAcknowledged's own doc comment for why this is separate from Status.
    Task<ServiceResult<OrderResponse>> AcknowledgeAsync(int id);

    Task<ServiceResult<bool>> DeleteAsync(int id);

    // For the admin "Create Order" screen's registered-customer phone search.
    Task<ServiceResult<CustomerLookupResponse>> LookupCustomerByPhoneAsync(string phoneNumber);
}
