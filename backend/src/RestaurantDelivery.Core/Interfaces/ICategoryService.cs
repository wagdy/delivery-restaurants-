using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Categories;

namespace RestaurantDelivery.Core.Interfaces;

public interface ICategoryService
{
    Task<List<CategoryResponse>> GetAllAsync();
    Task<ServiceResult<CategoryResponse>> CreateAsync(CategoryRequest request);
    Task<ServiceResult<CategoryResponse>> UpdateAsync(int id, CategoryRequest request);
    Task<ServiceResult<bool>> DeleteAsync(int id);
    Task<ServiceResult<bool>> ReorderAsync(List<int> orderedIds);
}
