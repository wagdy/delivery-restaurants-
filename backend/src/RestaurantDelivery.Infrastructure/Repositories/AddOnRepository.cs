using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Interfaces;
using RestaurantDelivery.Infrastructure.Data;

namespace RestaurantDelivery.Infrastructure.Repositories;

public class AddOnRepository : GenericRepository<AddOn>, IAddOnRepository
{
    public AddOnRepository(ApplicationDbContext context) : base(context)
    {
    }

    public Task<List<AddOn>> GetAllOrderedAsync() =>
        DbSet.OrderBy(a => a.Name).ToListAsync();

    public Task<List<AddOn>> GetByIdsAsync(IEnumerable<int> ids) =>
        DbSet.Where(a => ids.Contains(a.Id)).ToListAsync();

    public Task<AddOn?> GetByNameAsync(string name) =>
        DbSet.FirstOrDefaultAsync(a => a.Name.ToLower() == name.ToLower());

    public Task<int> CountOrderUsageAsync(int addOnId) =>
        Context.Set<OrderItemAddOn>().CountAsync(oa => oa.AddOnId == addOnId);
}
