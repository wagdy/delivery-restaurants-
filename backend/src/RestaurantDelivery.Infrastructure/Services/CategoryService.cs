using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Categories;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Infrastructure.Services;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _repository;

    public CategoryService(ICategoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<CategoryResponse>> GetAllAsync()
    {
        var categories = await _repository.GetAllOrderedAsync();
        return categories.Select(MapResponse).ToList();
    }

    public async Task<ServiceResult<CategoryResponse>> CreateAsync(CategoryRequest request)
    {
        var name = request.Name.Trim();

        if (await _repository.GetByNameAsync(name) is not null)
        {
            return ServiceResult<CategoryResponse>.Failure("A category with this name already exists.");
        }

        var existingCategories = await _repository.GetAllOrderedAsync();
        var nextDisplayOrder = existingCategories.Count == 0 ? 0 : existingCategories[^1].DisplayOrder + 1;

        var category = new Category { Name = name, DisplayOrder = nextDisplayOrder };
        await _repository.AddAsync(category);
        await _repository.SaveChangesAsync();

        return ServiceResult<CategoryResponse>.Success(MapResponse(category));
    }

    public async Task<ServiceResult<CategoryResponse>> UpdateAsync(int id, CategoryRequest request)
    {
        var category = await _repository.GetByIdAsync(id);
        if (category is null)
        {
            return ServiceResult<CategoryResponse>.Failure("Category not found.");
        }

        var name = request.Name.Trim();

        var existing = await _repository.GetByNameAsync(name);
        if (existing is not null && existing.Id != id)
        {
            return ServiceResult<CategoryResponse>.Failure("A category with this name already exists.");
        }

        if (category.Name != name)
        {
            await _repository.RenameMenuItemsCategoryAsync(category.Name, name);
            category.Name = name;
        }

        await _repository.SaveChangesAsync();

        return ServiceResult<CategoryResponse>.Success(MapResponse(category));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id)
    {
        var category = await _repository.GetByIdAsync(id);
        if (category is null)
        {
            return ServiceResult<bool>.Failure("Category not found.");
        }

        var itemCount = await _repository.CountMenuItemsInCategoryAsync(category.Name);
        if (itemCount > 0)
        {
            return ServiceResult<bool>.Failure(
                $"Cannot delete this category because {itemCount} menu item(s) still use it. Reassign or delete them first.");
        }

        _repository.Remove(category);
        await _repository.SaveChangesAsync();

        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> ReorderAsync(List<int> orderedIds)
    {
        if (orderedIds.Distinct().Count() != orderedIds.Count)
        {
            return ServiceResult<bool>.Failure("Duplicate category IDs in reorder request.");
        }

        var categories = await _repository.GetByIdsAsync(orderedIds);
        if (categories.Count != orderedIds.Count)
        {
            return ServiceResult<bool>.Failure("One or more categories were not found.");
        }

        var allCategories = await _repository.GetAllOrderedAsync();
        if (categories.Count != allCategories.Count)
        {
            return ServiceResult<bool>.Failure("The reorder request must include every category exactly once.");
        }

        var categoriesById = categories.ToDictionary(c => c.Id);
        for (var index = 0; index < orderedIds.Count; index++)
        {
            categoriesById[orderedIds[index]].DisplayOrder = index;
        }

        // One SaveChangesAsync call commits every DisplayOrder update together in a
        // single transaction - either the whole new order lands, or none of it does.
        await _repository.SaveChangesAsync();

        return ServiceResult<bool>.Success(true);
    }

    private static CategoryResponse MapResponse(Category category) => new()
    {
        Id = category.Id,
        Name = category.Name,
        DisplayOrder = category.DisplayOrder
    };
}
