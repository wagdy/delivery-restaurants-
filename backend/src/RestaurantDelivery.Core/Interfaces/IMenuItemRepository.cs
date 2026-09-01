using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Core.Interfaces;

public interface IMenuItemRepository : IGenericRepository<MenuItem>
{
    Task<List<MenuItem>> GetFilteredAsync(string? category, string? searchQuery, bool? isAvailable, bool? hasAddons);
    Task<MenuItem?> GetByIdWithAddOnsAsync(int id);
}
