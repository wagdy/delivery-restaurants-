using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Interfaces;
using RestaurantDelivery.Infrastructure.Data;

namespace RestaurantDelivery.Infrastructure.Repositories;

public class MenuItemRepository : GenericRepository<MenuItem>, IMenuItemRepository
{
    public MenuItemRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<List<MenuItem>> GetFilteredAsync(string? category, string? searchQuery, bool? isAvailable, bool? hasAddons)
    {
        var query = DbSet
            .Include(m => m.MenuItemAddOns)
            .ThenInclude(ma => ma.AddOn)
            .AsQueryable();

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
            .FirstOrDefaultAsync(m => m.Id == id);
}
