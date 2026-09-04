using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.SubCategories;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Infrastructure.Services;

public class SubCategoryService : ISubCategoryService
{
    private readonly ISubCategoryRepository _repository;
    private readonly ICategoryRepository _categoryRepository;

    public SubCategoryService(ISubCategoryRepository repository, ICategoryRepository categoryRepository)
    {
        _repository = repository;
        _categoryRepository = categoryRepository;
    }

    public async Task<List<SubCategoryResponse>> GetAllAsync()
    {
        var subCategories = await _repository.GetAllOrderedAsync();
        return subCategories.Select(MapResponse).ToList();
    }

    public async Task<ServiceResult<SubCategoryResponse>> CreateAsync(SubCategoryRequest request)
    {
        var category = await _categoryRepository.GetByIdAsync(request.CategoryId);
        if (category is null)
        {
            return ServiceResult<SubCategoryResponse>.Failure("Category not found.");
        }

        var name = request.Name.Trim();

        if (await _repository.GetByNameInCategoryAsync(request.CategoryId, name) is not null)
        {
            return ServiceResult<SubCategoryResponse>.Failure("A sub-category with this name already exists in this category.");
        }

        var existingInCategory = await _repository.GetByCategoryIdOrderedAsync(request.CategoryId);
        var nextDisplayOrder = existingInCategory.Count == 0 ? 0 : existingInCategory[^1].DisplayOrder + 1;

        var subCategory = new SubCategory { Name = name, CategoryId = request.CategoryId, DisplayOrder = nextDisplayOrder };
        await _repository.AddAsync(subCategory);
        await _repository.SaveChangesAsync();

        return ServiceResult<SubCategoryResponse>.Success(MapResponse(subCategory));
    }

    public async Task<ServiceResult<SubCategoryResponse>> UpdateAsync(int id, SubCategoryRequest request)
    {
        var subCategory = await _repository.GetByIdAsync(id);
        if (subCategory is null)
        {
            return ServiceResult<SubCategoryResponse>.Failure("Sub-category not found.");
        }

        var category = await _categoryRepository.GetByIdAsync(request.CategoryId);
        if (category is null)
        {
            return ServiceResult<SubCategoryResponse>.Failure("Category not found.");
        }

        var name = request.Name.Trim();

        var existing = await _repository.GetByNameInCategoryAsync(request.CategoryId, name);
        if (existing is not null && existing.Id != id)
        {
            return ServiceResult<SubCategoryResponse>.Failure("A sub-category with this name already exists in this category.");
        }

        subCategory.Name = name;
        subCategory.CategoryId = request.CategoryId;

        await _repository.SaveChangesAsync();

        return ServiceResult<SubCategoryResponse>.Success(MapResponse(subCategory));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id)
    {
        var subCategory = await _repository.GetByIdAsync(id);
        if (subCategory is null)
        {
            return ServiceResult<bool>.Failure("Sub-category not found.");
        }

        var itemCount = await _repository.CountMenuItemsInSubCategoryAsync(id);
        if (itemCount > 0)
        {
            return ServiceResult<bool>.Failure(
                $"Cannot delete this sub-category because {itemCount} menu item(s) still use it. Reassign or delete them first.");
        }

        _repository.Remove(subCategory);
        await _repository.SaveChangesAsync();

        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> ReorderAsync(ReorderSubCategoriesRequest request)
    {
        if (request.OrderedIds.Distinct().Count() != request.OrderedIds.Count)
        {
            return ServiceResult<bool>.Failure("Duplicate sub-category IDs in reorder request.");
        }

        var subCategories = await _repository.GetByIdsAsync(request.OrderedIds);
        if (subCategories.Count != request.OrderedIds.Count || subCategories.Any(sc => sc.CategoryId != request.CategoryId))
        {
            return ServiceResult<bool>.Failure("One or more sub-categories were not found in this category.");
        }

        var allInCategory = await _repository.GetByCategoryIdOrderedAsync(request.CategoryId);
        if (subCategories.Count != allInCategory.Count)
        {
            return ServiceResult<bool>.Failure("The reorder request must include every sub-category in this category exactly once.");
        }

        var subCategoriesById = subCategories.ToDictionary(sc => sc.Id);
        for (var index = 0; index < request.OrderedIds.Count; index++)
        {
            subCategoriesById[request.OrderedIds[index]].DisplayOrder = index;
        }

        await _repository.SaveChangesAsync();

        return ServiceResult<bool>.Success(true);
    }

    private static SubCategoryResponse MapResponse(SubCategory subCategory) => new()
    {
        Id = subCategory.Id,
        Name = subCategory.Name,
        DisplayOrder = subCategory.DisplayOrder,
        CategoryId = subCategory.CategoryId
    };
}
