using RestaurantDelivery.Core.DTOs.MenuItems;
using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Core.Interfaces;

public interface IMenuItemRepository : IGenericRepository<MenuItem>
{
    Task<List<MenuItem>> GetByIdsAsync(List<int> ids);
    Task<List<MenuItem>> GetByIdsIncludingDeletedAsync(List<int> ids);
    Task<List<MenuItem>> GetByCategoryAsync(string categoryName);
    Task<List<MenuItem>> GetFilteredAsync(string? category, string? searchQuery, bool? isAvailable, bool? hasAddons, DeletedFilter deleted);
    Task<MenuItem?> GetByIdWithAddOnsAsync(int id);
    Task<MenuItem?> GetByNameAsync(string name);
}
