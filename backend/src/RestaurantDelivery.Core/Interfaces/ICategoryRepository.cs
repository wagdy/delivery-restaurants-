using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Core.Interfaces;

public interface ICategoryRepository : IGenericRepository<Category>
{
    Task<List<Category>> GetAllOrderedAsync();
    Task<List<Category>> GetByIdsAsync(List<int> ids);
    Task<Category?> GetByNameAsync(string name);
    Task<List<Category>> GetDeletedByNamesAsync(List<string> names);
    Task<int> RenameMenuItemsCategoryAsync(string oldName, string newName);
    Task<int> CountMenuItemsInCategoryAsync(string name);
}
