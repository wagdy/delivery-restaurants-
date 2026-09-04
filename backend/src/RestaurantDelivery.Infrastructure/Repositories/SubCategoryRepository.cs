using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Interfaces;
using RestaurantDelivery.Infrastructure.Data;

namespace RestaurantDelivery.Infrastructure.Repositories;

public class SubCategoryRepository : GenericRepository<SubCategory>, ISubCategoryRepository
{
    public SubCategoryRepository(ApplicationDbContext context) : base(context)
    {
    }

    public Task<List<SubCategory>> GetAllOrderedAsync() =>
        DbSet.OrderBy(sc => sc.CategoryId).ThenBy(sc => sc.DisplayOrder).ToListAsync();

    public Task<List<SubCategory>> GetByCategoryIdOrderedAsync(int categoryId) =>
        DbSet.Where(sc => sc.CategoryId == categoryId).OrderBy(sc => sc.DisplayOrder).ToListAsync();

    public Task<List<SubCategory>> GetByIdsAsync(List<int> ids) =>
        DbSet.Where(sc => ids.Contains(sc.Id)).ToListAsync();

    public Task<SubCategory?> GetByNameInCategoryAsync(int categoryId, string name) =>
        DbSet.FirstOrDefaultAsync(sc => sc.CategoryId == categoryId && sc.Name.ToLower() == name.ToLower());

    public Task<int> CountMenuItemsInSubCategoryAsync(int subCategoryId) =>
        Context.Set<MenuItem>().CountAsync(m => m.SubCategoryId == subCategoryId);
}
