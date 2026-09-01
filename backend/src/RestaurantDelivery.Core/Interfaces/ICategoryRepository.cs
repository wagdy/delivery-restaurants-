using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Core.Interfaces;

public interface ICategoryRepository : IGenericRepository<Category>
{
    Task<List<Category>> GetAllOrderedAsync();
    Task<Category?> GetByNameAsync(string name);
    Task<int> RenameMenuItemsCategoryAsync(string oldName, string newName);
    Task<int> CountMenuItemsInCategoryAsync(string name);
}
