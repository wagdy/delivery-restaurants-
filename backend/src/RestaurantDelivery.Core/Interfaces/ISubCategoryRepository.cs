using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Core.Interfaces;

public interface ISubCategoryRepository : IGenericRepository<SubCategory>
{
    Task<List<SubCategory>> GetAllOrderedAsync();
    Task<List<SubCategory>> GetByCategoryIdOrderedAsync(int categoryId);
    Task<List<SubCategory>> GetByIdsAsync(List<int> ids);
    Task<SubCategory?> GetByNameInCategoryAsync(int categoryId, string name);
    Task<int> CountMenuItemsInSubCategoryAsync(int subCategoryId);
}
