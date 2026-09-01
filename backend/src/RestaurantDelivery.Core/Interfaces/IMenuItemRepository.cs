using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Core.Interfaces;

public interface IMenuItemRepository : IGenericRepository<MenuItem>
{
    Task<List<MenuItem>> GetFilteredAsync(string? category, string? search, bool? isAvailable);
    Task<MenuItem?> GetByIdWithAddOnsAsync(int id);
}
