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

    public MenuItemService(
        IMenuItemRepository repository,
        IAddOnRepository addOnRepository,
        ICategoryRepository categoryRepository)
    {
        _repository = repository;
        _addOnRepository = addOnRepository;
        _categoryRepository = categoryRepository;
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

        var item = new MenuItem
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Category = request.Category,
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

        item.Name = request.Name;
        item.Description = request.Description;
        item.Price = request.Price;
        item.Category = request.Category;
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

    private static MenuItemResponse MapResponse(MenuItem item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        Description = item.Description,
        Price = item.Price,
        Category = item.Category,
        ImageUrl = item.ImageUrl,
        IsAvailable = item.IsAvailable,
        AddOns = item.MenuItemAddOns
            .Select(ma => new AddOnResponse { Id = ma.AddOn.Id, Name = ma.AddOn.Name, Price = ma.AddOn.Price })
            .OrderBy(a => a.Name)
            .ToList()
    };
}
