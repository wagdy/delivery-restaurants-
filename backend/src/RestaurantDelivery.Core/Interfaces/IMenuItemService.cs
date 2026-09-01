using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.MenuItems;

namespace RestaurantDelivery.Core.Interfaces;

public interface IMenuItemService
{
    Task<List<MenuItemResponse>> GetAllAsync(string? category, string? search, bool? isAvailable);
    Task<ServiceResult<MenuItemResponse>> GetByIdAsync(int id);
    Task<ServiceResult<MenuItemResponse>> CreateAsync(MenuItemRequest request);
    Task<ServiceResult<MenuItemResponse>> UpdateAsync(int id, MenuItemRequest request);
    Task<ServiceResult<bool>> DeleteAsync(int id);
}
