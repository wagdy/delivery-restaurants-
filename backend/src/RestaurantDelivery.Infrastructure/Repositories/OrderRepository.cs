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
            .IgnoreQueryFilters()
            .Include(o => o.OrderItems).ThenInclude(oi => oi.MenuItem)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.AddOns)
            .FirstOrDefaultAsync(o => o.Id == id);

    public Task<Order?> GetByExternalIdAsync(string externalSource, string externalOrderId) =>
        DbSet
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.ExternalSource == externalSource && o.ExternalOrderId == externalOrderId);

    public async Task<(List<Order> Orders, int TotalCount)> GetPagedWithItemsAsync(OrderStatus? status, int page, int pageSize)
    {
        var query = DbSet
            .IgnoreQueryFilters()
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
            .IgnoreQueryFilters()
            .Include(o => o.OrderItems).ThenInclude(oi => oi.MenuItem)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.AddOns)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

    // Variants are included for the same reason add-ons are: BuildOrderItemsAsync prices
    // and validates against them. Without the Include, Variants comes back empty, the
    // "please choose a size" check never fires, and every variant item is quietly
    // charged at its placeholder base price.
    public Task<List<MenuItem>> GetMenuItemsByIdsAsync(IEnumerable<int> ids) =>
        Context.Set<MenuItem>()
            .Include(m => m.MenuItemAddOns).ThenInclude(ma => ma.AddOn)
            .Include(m => m.Variants)
            .Where(m => ids.Contains(m.Id))
            .ToListAsync();

    public Task<AppUser?> GetCustomerByPhoneAsync(string phoneNumber) =>
        Context.Set<AppUser>().FirstOrDefaultAsync(u => u.Role == UserRole.Customer && !u.IsDeleted && u.PhoneNumber == phoneNumber);

    public Task<AppUser?> GetCustomerByIdAsync(string customerId) =>
        Context.Set<AppUser>().FirstOrDefaultAsync(u => u.Role == UserRole.Customer && !u.IsDeleted && u.Id == customerId);
}
