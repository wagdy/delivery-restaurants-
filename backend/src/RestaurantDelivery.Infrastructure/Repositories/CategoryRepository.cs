using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Interfaces;
using RestaurantDelivery.Infrastructure.Data;

namespace RestaurantDelivery.Infrastructure.Repositories;

public class CategoryRepository : GenericRepository<Category>, ICategoryRepository
{
    public CategoryRepository(ApplicationDbContext context) : base(context)
    {
    }

    public Task<List<Category>> GetAllOrderedAsync() =>
        DbSet.OrderBy(c => c.DisplayOrder).ToListAsync();

    public Task<List<Category>> GetByIdsAsync(List<int> ids) =>
        DbSet.Where(c => ids.Contains(c.Id)).ToListAsync();

    public Task<Category?> GetByNameAsync(string name) =>
        DbSet.FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower());

    public async Task<int> RenameMenuItemsCategoryAsync(string oldName, string newName)
    {
        var items = await Context.Set<MenuItem>().Where(m => m.Category == oldName).ToListAsync();
        foreach (var item in items)
        {
            item.Category = newName;
        }
        return items.Count;
    }

    public Task<int> CountMenuItemsInCategoryAsync(string name) =>
        Context.Set<MenuItem>().CountAsync(m => m.Category == name);
}
