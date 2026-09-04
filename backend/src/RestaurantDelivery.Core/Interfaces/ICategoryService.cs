using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Categories;

namespace RestaurantDelivery.Core.Interfaces;

public interface ICategoryService
{
    Task<List<CategoryResponse>> GetAllAsync();
    Task<ServiceResult<CategoryResponse>> CreateAsync(CategoryRequest request);

    // updateImage=false leaves the category's existing ImageUrl untouched regardless of
    // request.ImageUrl - the caller (CategoriesController) only ever sets this true when
    // an admin actually selected a new file to upload.
    Task<ServiceResult<CategoryResponse>> UpdateAsync(int id, CategoryRequest request, bool updateImage);
    Task<ServiceResult<bool>> DeleteAsync(int id);
    Task<ServiceResult<bool>> ReorderAsync(List<int> orderedIds);
}
