using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Categories;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Infrastructure.Services;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _repository;
    private readonly IReadThroughCache _cache;

    public CategoryService(ICategoryRepository repository, IReadThroughCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task<List<CategoryResponse>> GetAllAsync()
    {
        // Read on every storefront page load and changed only when an admin edits the
        // category list, so it is served from cache and dropped by the writes below.
        return await _cache.GetOrCreateAsync(CacheGroup.Categories, "all", async () =>
        {
            var categories = await _repository.GetAllOrderedAsync();
            return categories.Select(MapResponse).ToList();
        });
    }

    // Every category write drops the menu cache as well as the category cache: renaming a
    // category rewrites MenuItem.Category on every item under it (see
    // RenameMenuItemsCategoryAsync), so a stale menu would still be grouped under the old
    // name. Deleting or reordering changes what the storefront shows just as directly.
    private void InvalidateCategoryCaches()
    {
        _cache.Invalidate(CacheGroup.Categories);
        _cache.Invalidate(CacheGroup.Menu);
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

        var category = new Category
        {
            Name = name,
            NameAr = OptionalText.NullIfBlank(request.NameAr),
            DisplayOrder = nextDisplayOrder,
            ImageUrl = request.ImageUrl
        };
        await _repository.AddAsync(category);
        await _repository.SaveChangesAsync();
        InvalidateCategoryCaches();

        return ServiceResult<CategoryResponse>.Success(MapResponse(category));
    }

    public async Task<ServiceResult<CategoryResponse>> UpdateAsync(int id, CategoryRequest request, bool updateImage)
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

        // No cascade needed for the Arabic name: MenuItem.Category stores the English
        // name as its free-text link (hence RenameMenuItemsCategoryAsync above), and
        // nothing anywhere keys off NameAr.
        category.NameAr = OptionalText.NullIfBlank(request.NameAr);

        if (updateImage)
        {
            category.ImageUrl = request.ImageUrl;
        }

        await _repository.SaveChangesAsync();
        InvalidateCategoryCaches();

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
        InvalidateCategoryCaches();

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
        InvalidateCategoryCaches();

        return ServiceResult<bool>.Success(true);
    }

    private static CategoryResponse MapResponse(Category category) => new()
    {
        Id = category.Id,
        Name = category.Name,
        NameAr = category.NameAr,
        DisplayOrder = category.DisplayOrder,
        ImageUrl = category.ImageUrl
    };
}
