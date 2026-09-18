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

    // AsNoTracking: all three callers read only - GetAllAsync maps to DTOs, CreateAsync
    // reads the last DisplayOrder to pick the next one, and ReorderAsync uses it purely
    // for a count check. The reorder's actual DisplayOrder writes go through
    // GetByIdsAsync below, which is why that one stays tracked.
    public Task<List<Category>> GetAllOrderedAsync() =>
        DbSet.AsNoTracking().OrderBy(c => c.DisplayOrder).ToListAsync();

    // Tracked on purpose: CategoryService.ReorderAsync mutates DisplayOrder on these
    // entities and relies on the change tracker to persist it.
    public Task<List<Category>> GetByIdsAsync(List<int> ids) =>
        DbSet.Where(c => ids.Contains(c.Id)).ToListAsync();

    // Used only by the restore path, which needs to see past the soft-delete filter -
    // every row it wants is one the filter is hiding.
    public async Task<List<Category>> GetDeletedByNamesAsync(List<string> names) =>
        await DbSet.IgnoreQueryFilters().Where(c => c.IsDeleted && names.Contains(c.Name)).ToListAsync();

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
