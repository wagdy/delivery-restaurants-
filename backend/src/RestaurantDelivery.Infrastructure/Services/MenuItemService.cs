using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.AddOns;
using RestaurantDelivery.Core.DTOs.MenuItems;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Interfaces;
using RestaurantDelivery.Core.DTOs.Common;

namespace RestaurantDelivery.Infrastructure.Services;

public class MenuItemService : IMenuItemService
{
    private readonly IMenuItemRepository _repository;
    private readonly IAddOnRepository _addOnRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ISubCategoryRepository _subCategoryRepository;

    private readonly IReadThroughCache _cache;

    public MenuItemService(
        IMenuItemRepository repository,
        IAddOnRepository addOnRepository,
        ICategoryRepository categoryRepository,
        ISubCategoryRepository subCategoryRepository,
        IReadThroughCache cache)
    {
        _repository = repository;
        _addOnRepository = addOnRepository;
        _categoryRepository = categoryRepository;
        _subCategoryRepository = subCategoryRepository;
        _cache = cache;
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

        // The app's hottest read: the whole menu, on every storefront page load, for every
        // visitor. Cached on everything except a search, which is deliberately left to go
        // straight to the database - search text is free-form, so caching per term would
        // let any caller grow the cache (and ReadThroughCache's per-key locks) without
        // limit. The remaining key space is category x availability x has-addons: a few
        // dozen combinations at most.
        //
        // A query that asks for deleted rows skips the cache entirely rather than adding
        // a fourth dimension to the key. That is not an optimisation, it is a safety
        // rule: this cache is shared with the storefront, so a key that did not account
        // for Deleted would let one admin opening "Show deleted" publish deleted items
        // to every customer until the entry expired. Admin recovery traffic is rare
        // enough that reading straight through costs nothing.
        if (!string.IsNullOrWhiteSpace(filter.SearchQuery) || filter.Deleted != DeletedFilter.Active)
        {
            var uncached = await _repository.GetFilteredAsync(
                categoryName, filter.SearchQuery, filter.IsAvailable, filter.HasAddons, filter.Deleted);
            return uncached.Select(MapResponse).ToList();
        }

        var cacheKey = $"{categoryName ?? "*"}|{filter.IsAvailable?.ToString() ?? "*"}|{filter.HasAddons?.ToString() ?? "*"}";

        return await _cache.GetOrCreateAsync(CacheGroup.Menu, cacheKey, async () =>
        {
            var items = await _repository.GetFilteredAsync(
                categoryName, null, filter.IsAvailable, filter.HasAddons, DeletedFilter.Active);
            return items.Select(MapResponse).ToList();
        });
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
            NameAr = OptionalText.NullIfBlank(request.NameAr),
            Description = request.Description,
            Price = request.Price,
            Category = request.Category,
            SubCategory = subCategoryResult.Data,
            ImageUrl = request.ImageUrl,
            IsAvailable = request.IsAvailable,
            MenuItemAddOns = addOnsResult.Data!.Select(a => new MenuItemAddOn { AddOn = a }).ToList(),
            Variants = request.Variants
                .Select(v => new MenuItemVariant
                {
                    Name = v.Name.Trim(),
                    NameAr = OptionalText.NullIfBlank(v.NameAr),
                    Price = v.Price,
                    DisplayOrder = v.DisplayOrder,
                    IsAvailable = v.IsAvailable
                })
                .ToList()
        };

        await _repository.AddAsync(item);
        await _repository.SaveChangesAsync();
        _cache.Invalidate(CacheGroup.Menu);

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
        item.NameAr = OptionalText.NullIfBlank(request.NameAr);
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

        SyncVariants(item, request.Variants);

        await _repository.SaveChangesAsync();
        _cache.Invalidate(CacheGroup.Menu);

        return ServiceResult<MenuItemResponse>.Success(MapResponse(item));
    }

    // Soft delete: the row stays, a global query filter hides it (see
    // ApplicationDbContext.OnModelCreating). Hard deleting was refused outright whenever
    // an item appeared on any past order - OrderItem -> MenuItem is DeleteBehavior.Restrict -
    // which is exactly the case an admin most wants to clear off the menu. The receipt is
    // unaffected because OrderItem snapshots the name at checkout.
    public async Task<ServiceResult<bool>> DeleteAsync(int id)
    {
        var item = await _repository.GetByIdAsync(id);
        if (item is null)
        {
            return ServiceResult<bool>.Failure("Menu item not found.");
        }

        item.IsDeleted = true;
        await _repository.SaveChangesAsync();
        _cache.Invalidate(CacheGroup.Menu);

        return ServiceResult<bool>.Success(true);
    }

    // Reports how many of the requested ids it actually changed rather than a bare
    // success: an id that is already gone, or was deleted by someone else a moment ago,
    // should not read as "deleted 12 items" when it deleted 11.
    private const int MaxBulkIds = 500;

    public async Task<ServiceResult<BulkActionResult>> BulkDeleteAsync(IReadOnlyCollection<int> ids)
    {
        if (ids.Count == 0)
        {
            return ServiceResult<BulkActionResult>.Failure("No menu items were selected.");
        }

        // The admin table can only tick rows it has loaded, so this is never hit from the
        // UI. It is here because the endpoint takes an arbitrary id list: without it, a
        // malformed request turns into a single IN clause with tens of thousands of
        // parameters and ties up a connection instead of being refused.
        if (ids.Count > MaxBulkIds)
        {
            return ServiceResult<BulkActionResult>.Failure(
                $"Too many items selected at once. Please select {MaxBulkIds} or fewer.");
        }

        var items = await _repository.GetByIdsAsync(ids.ToList());
        foreach (var item in items)
        {
            item.IsDeleted = true;
        }

        await _repository.SaveChangesAsync();
        _cache.Invalidate(CacheGroup.Menu);

        return ServiceResult<BulkActionResult>.Success(
            new BulkActionResult { Requested = ids.Count, Affected = items.Count });
    }

    // One explicit target rather than a per-item flip: "make these 12 unavailable" is a
    // predictable outcome, where toggling each independently leaves a mixed selection in
    // a state the admin cannot guess from the button they pressed.
    // The inverse of BulkDeleteAsync. Deliberately also lifts the item's category back
    // out of deletion when that category was deleted too: deleting a category cascades
    // to its items, so most restores start from exactly that case, and an item restored
    // into a category that is still hidden is invisible on the storefront and
    // unselectable in the admin category filter - a restore that appears to do nothing.
    // MenuItem.Category is free text, so the match is by name, same as the cascade.
    public async Task<ServiceResult<BulkActionResult>> BulkRestoreAsync(IReadOnlyCollection<int> ids)
    {
        if (ids.Count == 0)
        {
            return ServiceResult<BulkActionResult>.Failure("No menu items were selected.");
        }

        if (ids.Count > MaxBulkIds)
        {
            return ServiceResult<BulkActionResult>.Failure(
                $"Too many items selected at once. Please select {MaxBulkIds} or fewer.");
        }

        var items = await _repository.GetByIdsIncludingDeletedAsync(ids.ToList());
        foreach (var item in items)
        {
            item.IsDeleted = false;
        }

        var categoryNames = items
            .Select(i => i.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .ToList();

        if (categoryNames.Count > 0)
        {
            foreach (var category in await _categoryRepository.GetDeletedByNamesAsync(categoryNames))
            {
                category.IsDeleted = false;
            }
        }

        await _repository.SaveChangesAsync();
        _cache.Invalidate(CacheGroup.Menu);

        return ServiceResult<BulkActionResult>.Success(
            new BulkActionResult { Requested = ids.Count, Affected = items.Count });
    }

    public async Task<ServiceResult<BulkActionResult>> BulkSetAvailabilityAsync(
        IReadOnlyCollection<int> ids,
        bool isAvailable)
    {
        if (ids.Count == 0)
        {
            return ServiceResult<BulkActionResult>.Failure("No menu items were selected.");
        }

        // The admin table can only tick rows it has loaded, so this is never hit from the
        // UI. It is here because the endpoint takes an arbitrary id list: without it, a
        // malformed request turns into a single IN clause with tens of thousands of
        // parameters and ties up a connection instead of being refused.
        if (ids.Count > MaxBulkIds)
        {
            return ServiceResult<BulkActionResult>.Failure(
                $"Too many items selected at once. Please select {MaxBulkIds} or fewer.");
        }

        var items = await _repository.GetByIdsAsync(ids.ToList());
        foreach (var item in items)
        {
            item.IsAvailable = isAvailable;
        }

        await _repository.SaveChangesAsync();
        _cache.Invalidate(CacheGroup.Menu);

        return ServiceResult<BulkActionResult>.Success(
            new BulkActionResult { Requested = ids.Count, Affected = items.Count });
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

    // Updates variants in place rather than clear-and-re-add.
    //
    // Clearing would delete every row and insert new ones with new ids, which silently
    // breaks two things: the VariantId recorded on past OrderItems stops matching
    // anything for reporting, and a customer holding the item in their cart has a
    // variant id that no longer exists. Matching on Id keeps an edited "1 Kilo" the same
    // "1 Kilo" it was.
    private static void SyncVariants(MenuItem item, List<MenuItemVariantRequest> requested)
    {
        var keptIds = requested.Where(v => v.Id.HasValue).Select(v => v.Id!.Value).ToHashSet();

        foreach (var removed in item.Variants.Where(v => !keptIds.Contains(v.Id)).ToList())
        {
            item.Variants.Remove(removed);
        }

        foreach (var incoming in requested)
        {
            var existing = incoming.Id.HasValue
                ? item.Variants.FirstOrDefault(v => v.Id == incoming.Id.Value)
                : null;

            if (existing is null)
            {
                item.Variants.Add(new MenuItemVariant
                {
                    MenuItemId = item.Id,
                    Name = incoming.Name.Trim(),
                    NameAr = OptionalText.NullIfBlank(incoming.NameAr),
                    Price = incoming.Price,
                    DisplayOrder = incoming.DisplayOrder,
                    IsAvailable = incoming.IsAvailable
                });
                continue;
            }

            existing.Name = incoming.Name.Trim();
            existing.NameAr = OptionalText.NullIfBlank(incoming.NameAr);
            existing.Price = incoming.Price;
            existing.DisplayOrder = incoming.DisplayOrder;
            existing.IsAvailable = incoming.IsAvailable;
        }
    }

    private static MenuItemResponse MapResponse(MenuItem item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        NameAr = item.NameAr,
        Description = item.Description,
        Price = item.Price,
        Category = item.Category,
        SubCategoryId = item.SubCategoryId,
        SubCategoryName = item.SubCategory?.Name,
        ImageUrl = item.ImageUrl,
        IsAvailable = item.IsAvailable,
        IsDeleted = item.IsDeleted,
        AddOns = item.MenuItemAddOns
            .Select(ma => new AddOnResponse
            {
                Id = ma.AddOn.Id,
                Name = ma.AddOn.Name,
                NameAr = ma.AddOn.NameAr,
                Price = ma.AddOn.Price
            })
            // Ordered by the English name in both languages, deliberately: a stable,
            // predictable order beats one that reshuffles when the customer toggles
            // language, and add-on lists are short enough that alphabetical-in-Arabic
            // buys nothing.
            .OrderBy(a => a.Name)
            .ToList(),
        Variants = item.Variants
            .Select(v => new MenuItemVariantResponse
            {
                Id = v.Id,
                Name = v.Name,
                NameAr = v.NameAr,
                Price = v.Price,
                DisplayOrder = v.DisplayOrder,
                IsAvailable = v.IsAvailable
            })
            // DisplayOrder first so the admin controls the sequence, then price - which
            // means an admin who never touches DisplayOrder still gets cheapest-first,
            // matching what the client pre-selects.
            .OrderBy(v => v.DisplayOrder)
            .ThenBy(v => v.Price)
            .ToList()
    };
}
