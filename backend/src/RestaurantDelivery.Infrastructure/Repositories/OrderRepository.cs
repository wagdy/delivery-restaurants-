using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Enums;
using RestaurantDelivery.Core.Interfaces;
using RestaurantDelivery.Infrastructure.Data;

namespace RestaurantDelivery.Infrastructure.Repositories;

public class OrderRepository : GenericRepository<Order>, IOrderRepository
{
    public OrderRepository(ApplicationDbContext context) : base(context)
    {
    }

    public Task<Order?> GetByIdWithItemsAsync(int id) =>
        DbSet
            .Include(o => o.OrderItems).ThenInclude(oi => oi.MenuItem)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.AddOns)
            .FirstOrDefaultAsync(o => o.Id == id);

    public async Task<(List<Order> Orders, int TotalCount)> GetPagedWithItemsAsync(OrderStatus? status, int page, int pageSize)
    {
        var query = DbSet
            .Include(o => o.OrderItems).ThenInclude(oi => oi.MenuItem)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.AddOns)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        var totalCount = await query.CountAsync();

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (orders, totalCount);
    }

    public Task<List<Order>> GetByUserIdAsync(string userId) =>
        DbSet
            .Include(o => o.OrderItems).ThenInclude(oi => oi.MenuItem)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.AddOns)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

    public Task<List<MenuItem>> GetMenuItemsByIdsAsync(IEnumerable<int> ids) =>
        Context.Set<MenuItem>()
            .Include(m => m.MenuItemAddOns).ThenInclude(ma => ma.AddOn)
            .Where(m => ids.Contains(m.Id))
            .ToListAsync();
}
