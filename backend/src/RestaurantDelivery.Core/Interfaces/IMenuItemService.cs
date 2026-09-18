using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.MenuItems;
using RestaurantDelivery.Core.DTOs.Common;

namespace RestaurantDelivery.Core.Interfaces;

public interface IMenuItemService
{
    Task<List<MenuItemResponse>> GetAllAsync(MenuItemFilterRequest filter);
    Task<ServiceResult<MenuItemResponse>> GetByIdAsync(int id);
    Task<ServiceResult<MenuItemResponse>> CreateAsync(MenuItemRequest request);
    Task<ServiceResult<MenuItemResponse>> UpdateAsync(int id, MenuItemRequest request);
    Task<ServiceResult<bool>> DeleteAsync(int id);
    Task<ServiceResult<BulkActionResult>> BulkDeleteAsync(IReadOnlyCollection<int> ids);
    Task<ServiceResult<BulkActionResult>> BulkSetAvailabilityAsync(IReadOnlyCollection<int> ids, bool isAvailable);
}
