using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Core.DTOs.MenuItems;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Interfaces;
using RestaurantDelivery.Infrastructure.Data;

namespace RestaurantDelivery.Infrastructure.Repositories;

public class MenuItemRepository : GenericRepository<MenuItem>, IMenuItemRepository
{
    public MenuItemRepository(ApplicationDbContext context) : base(context)
    {
    }

    // Tracked, not AsNoTracking: both callers mutate what comes back (bulk delete sets
    // IsDeleted, bulk availability sets IsAvailable), and an untracked entity saves
    // silently without an error - the same trap this file's other comment warns about.
    //
    // DbSet carries the soft-delete query filter, so an id that is already deleted
    // simply is not returned. That is what makes the Requested vs Affected counts in
    // BulkActionResult meaningful rather than always equal.
    public async Task<List<MenuItem>> GetByIdsAsync(List<int> ids) =>
        await DbSet.Where(m => ids.Contains(m.Id)).ToListAsync();

    // The restore counterpart to GetByIdsAsync: it has to see past the query filter,
    // since by definition every row it is asked for is one the filter hides.
    public async Task<List<MenuItem>> GetByIdsIncludingDeletedAsync(List<int> ids) =>
        await DbSet.IgnoreQueryFilters().Where(m => ids.Contains(m.Id)).ToListAsync();

    // Tracked for the same reason - CategoryService cascades a soft delete onto these.
    // Matched by name because MenuItem.Category is free text, not a foreign key.
    public async Task<List<MenuItem>> GetByCategoryAsync(string categoryName) =>
        await DbSet.Where(m => m.Category == categoryName).ToListAsync();

    public async Task<List<MenuItem>> GetFilteredAsync(
        string? category,
        string? searchQuery,
        bool? isAvailable,
        bool? hasAddons,
        DeletedFilter deleted)
    {
        // AsNoTracking because every caller of this method maps straight to
        // MenuItemResponse and never writes back - MenuItemService.GetAllAsync is the only
        // one. This is the app's hottest query (the full menu with its add-ons and
        // sub-categories, on every storefront page load), so tracking several hundred
        // entities plus their join rows was pure overhead on a request that never saves.
        //
        // Deliberately NOT applied to GetByIdWithAddOnsAsync below: that one also backs
        // UpdateAsync, where the returned entity IS mutated and saved. Adding it there
        // would make edits silently do nothing.
        var query = DbSet
            .AsNoTracking()
            .Include(m => m.MenuItemAddOns)
            .ThenInclude(ma => ma.AddOn)
            .Include(m => m.Variants)
            .Include(m => m.SubCategory)
            .AsQueryable();

        if (deleted != DeletedFilter.Active)
        {
            // IgnoreQueryFilters is what makes deleted rows reachable at all; the Where
            // then narrows to only those. Active takes neither branch, so the ordinary
            // storefront query is byte for byte the query it was before.
            query = query.IgnoreQueryFilters();

            if (deleted == DeletedFilter.Deleted)
            {
                query = query.Where(m => m.IsDeleted);
            }
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(m => m.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            // Item name only - category has its own dedicated filter, so this doesn't
            // also match against m.Category the way the old client-side filter did.
            query = query.Where(m => EF.Functions.ILike(m.Name, $"%{searchQuery}%"));
        }

        if (isAvailable.HasValue)
        {
            query = query.Where(m => m.IsAvailable == isAvailable.Value);
        }

        if (hasAddons.HasValue)
        {
            query = hasAddons.Value
                ? query.Where(m => m.MenuItemAddOns.Any())
                : query.Where(m => !m.MenuItemAddOns.Any());
        }

        return await query.OrderBy(m => m.Category).ThenBy(m => m.Name).ToListAsync();
    }

    public Task<MenuItem?> GetByNameAsync(string name) =>
        DbSet.FirstOrDefaultAsync(m => m.Name.ToLower() == name.ToLower());

    public Task<MenuItem?> GetByIdWithAddOnsAsync(int id) =>
        DbSet
            .Include(m => m.MenuItemAddOns)
            .ThenInclude(ma => ma.AddOn)
            // Tracked, and this method also backs UpdateAsync - so the loaded Variants
            // collection is what SyncVariants mutates in place.
            .Include(m => m.Variants)
            .Include(m => m.SubCategory)
            .FirstOrDefaultAsync(m => m.Id == id);
}
