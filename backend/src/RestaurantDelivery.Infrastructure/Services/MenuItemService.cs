using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.AddOns;
using RestaurantDelivery.Core.DTOs.MenuItems;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Infrastructure.Services;

public class MenuItemService : IMenuItemService
{
    private readonly IMenuItemRepository _repository;
    private readonly IAddOnRepository _addOnRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ISubCategoryRepository _subCategoryRepository;

    public MenuItemService(
        IMenuItemRepository repository,
        IAddOnRepository addOnRepository,
        ICategoryRepository categoryRepository,
        ISubCategoryRepository subCategoryRepository)
    {
        _repository = repository;
        _addOnRepository = addOnRepository;
        _categoryRepository = categoryRepository;
        _subCategoryRepository = subCategoryRepository;
    }

    public async Task<List<MenuItemResponse>> GetAllAsync(MenuItemFilterRequest filter)
    {
        // MenuItem.Category is a free-text field, not a foreign key, so a categoryId
        // filter has to be resolved to that category's name first.
        string? categoryName = null;
        if (filter.CategoryId.HasValue)
        {
            var category = await _categoryRepository.GetByIdAsync(filter.CategoryId.Value);
            if (category is null)
            {
                // An unknown categoryId should return no results, not silently ignore
                // the filter and return everything.
                return new List<MenuItemResponse>();
            }

            categoryName = category.Name;
        }

        var items = await _repository.GetFilteredAsync(categoryName, filter.SearchQuery, filter.IsAvailable, filter.HasAddons);
        return items.Select(MapResponse).ToList();
    }

    public async Task<ServiceResult<MenuItemResponse>> GetByIdAsync(int id)
    {
        var item = await _repository.GetByIdWithAddOnsAsync(id);
        if (item is null)
        {
            return ServiceResult<MenuItemResponse>.Failure("Menu item not found.");
        }

        return ServiceResult<MenuItemResponse>.Success(MapResponse(item));
    }

    public async Task<ServiceResult<MenuItemResponse>> CreateAsync(MenuItemRequest request)
    {
        var addOnsResult = await ResolveAddOnsAsync(request.AddOnIds);
        if (!addOnsResult.Succeeded)
        {
            return ServiceResult<MenuItemResponse>.Failure(addOnsResult.Errors.ToArray());
        }

        var subCategoryResult = await ResolveSubCategoryAsync(request.SubCategoryId, request.Category);
        if (!subCategoryResult.Succeeded)
        {
            return ServiceResult<MenuItemResponse>.Failure(subCategoryResult.Errors.ToArray());
        }

        var item = new MenuItem
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Category = request.Category,
            SubCategory = subCategoryResult.Data,
            ImageUrl = request.ImageUrl,
            IsAvailable = request.IsAvailable,
            MenuItemAddOns = addOnsResult.Data!.Select(a => new MenuItemAddOn { AddOn = a }).ToList()
        };

        await _repository.AddAsync(item);
        await _repository.SaveChangesAsync();

        return ServiceResult<MenuItemResponse>.Success(MapResponse(item));
    }

    public async Task<ServiceResult<MenuItemResponse>> UpdateAsync(int id, MenuItemRequest request)
    {
        var item = await _repository.GetByIdWithAddOnsAsync(id);
        if (item is null)
        {
            return ServiceResult<MenuItemResponse>.Failure("Menu item not found.");
        }

        var addOnsResult = await ResolveAddOnsAsync(request.AddOnIds);
        if (!addOnsResult.Succeeded)
        {
            return ServiceResult<MenuItemResponse>.Failure(addOnsResult.Errors.ToArray());
        }

        var subCategoryResult = await ResolveSubCategoryAsync(request.SubCategoryId, request.Category);
        if (!subCategoryResult.Succeeded)
        {
            return ServiceResult<MenuItemResponse>.Failure(subCategoryResult.Errors.ToArray());
        }

        item.Name = request.Name;
        item.Description = request.Description;
        item.Price = request.Price;
        item.Category = request.Category;
        item.SubCategory = subCategoryResult.Data;
        item.ImageUrl = request.ImageUrl;
        item.IsAvailable = request.IsAvailable;

        item.MenuItemAddOns.Clear();
        foreach (var addOn in addOnsResult.Data!)
        {
            item.MenuItemAddOns.Add(new MenuItemAddOn { MenuItemId = item.Id, AddOnId = addOn.Id, AddOn = addOn });
        }

        await _repository.SaveChangesAsync();

        return ServiceResult<MenuItemResponse>.Success(MapResponse(item));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id)
    {
        var item = await _repository.GetByIdAsync(id);
        if (item is null)
        {
            return ServiceResult<bool>.Failure("Menu item not found.");
        }

        _repository.Remove(item);

        try
        {
            await _repository.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ServiceResult<bool>.Failure(
                "Cannot delete this menu item because it is referenced by existing orders. Mark it unavailable instead.");
        }

        return ServiceResult<bool>.Success(true);
    }

    private async Task<ServiceResult<List<AddOn>>> ResolveAddOnsAsync(List<int> addOnIds)
    {
        if (addOnIds.Count == 0)
        {
            return ServiceResult<List<AddOn>>.Success(new List<AddOn>());
        }

        var distinctIds = addOnIds.Distinct().ToList();
        var addOns = await _addOnRepository.GetByIdsAsync(distinctIds);

        if (addOns.Count != distinctIds.Count)
        {
            return ServiceResult<List<AddOn>>.Failure("One or more selected add-ons were not found.");
        }

        return ServiceResult<List<AddOn>>.Success(addOns);
    }

    // Null subCategoryId is always valid (no sub-category assigned). A non-null one must
    // both exist and belong to the same category the item is being filed under - without
    // this second check, an admin switching an item's top-level Category via the free-
    // text dropdown could silently leave it pointing at another category's sub-category.
    private async Task<ServiceResult<SubCategory?>> ResolveSubCategoryAsync(int? subCategoryId, string categoryName)
    {
        if (subCategoryId is null)
        {
            return ServiceResult<SubCategory?>.Success(null);
        }

        var subCategory = await _subCategoryRepository.GetByIdAsync(subCategoryId.Value);
        if (subCategory is null)
        {
            return ServiceResult<SubCategory?>.Failure("Selected sub-category was not found.");
        }

        var category = await _categoryRepository.GetByIdAsync(subCategory.CategoryId);
        if (category is null || !string.Equals(category.Name, categoryName, StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<SubCategory?>.Failure("Selected sub-category does not belong to this item's category.");
        }

        return ServiceResult<SubCategory?>.Success(subCategory);
    }

    private static MenuItemResponse MapResponse(MenuItem item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        Description = item.Description,
        Price = item.Price,
        Category = item.Category,
        SubCategoryId = item.SubCategoryId,
        SubCategoryName = item.SubCategory?.Name,
        ImageUrl = item.ImageUrl,
        IsAvailable = item.IsAvailable,
        AddOns = item.MenuItemAddOns
            .Select(ma => new AddOnResponse { Id = ma.AddOn.Id, Name = ma.AddOn.Name, Price = ma.AddOn.Price })
            .OrderBy(a => a.Name)
            .ToList()
    };
}
