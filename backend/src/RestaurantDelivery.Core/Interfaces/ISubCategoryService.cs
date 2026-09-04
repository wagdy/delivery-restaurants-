using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.SubCategories;

namespace RestaurantDelivery.Core.Interfaces;

public interface ISubCategoryService
{
    Task<List<SubCategoryResponse>> GetAllAsync();
    Task<ServiceResult<SubCategoryResponse>> CreateAsync(SubCategoryRequest request);
    Task<ServiceResult<SubCategoryResponse>> UpdateAsync(int id, SubCategoryRequest request);
    Task<ServiceResult<bool>> DeleteAsync(int id);
    Task<ServiceResult<bool>> ReorderAsync(ReorderSubCategoriesRequest request);
}
