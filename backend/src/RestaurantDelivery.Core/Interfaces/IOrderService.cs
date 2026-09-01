using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Common;
using RestaurantDelivery.Core.DTOs.Orders;
using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.Interfaces;

public interface IOrderService
{
    Task<ServiceResult<OrderResponse>> CreateAsync(CreateOrderRequest request, string? userId);
    Task<ServiceResult<PagedResult<OrderResponse>>> GetAllAsync(OrderStatus? status, int page, int pageSize);
    Task<ServiceResult<OrderResponse>> GetByIdAsync(int id);
    Task<ServiceResult<List<OrderResponse>>> GetMyOrdersAsync(string userId);
    Task<ServiceResult<OrderResponse>> UpdateAsync(int id, UpdateOrderRequest request);
    Task<ServiceResult<OrderResponse>> UpdateStatusAsync(int id, OrderStatus status);
    Task<ServiceResult<bool>> DeleteAsync(int id);
}
