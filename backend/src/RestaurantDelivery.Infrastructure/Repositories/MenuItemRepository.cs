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

    public async Task<List<MenuItem>> GetFilteredAsync(string? category, string? search, bool? isAvailable)
    {
        var query = DbSet
            .Include(m => m.MenuItemAddOns)
            .ThenInclude(ma => ma.AddOn)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(m => m.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(m => EF.Functions.ILike(m.Name, $"%{search}%"));
        }

        if (isAvailable.HasValue)
        {
            query = query.Where(m => m.IsAvailable == isAvailable.Value);
        }

        return await query.OrderBy(m => m.Category).ThenBy(m => m.Name).ToListAsync();
    }

    public Task<MenuItem?> GetByIdWithAddOnsAsync(int id) =>
        DbSet
            .Include(m => m.MenuItemAddOns)
            .ThenInclude(ma => ma.AddOn)
            .FirstOrDefaultAsync(m => m.Id == id);
}
