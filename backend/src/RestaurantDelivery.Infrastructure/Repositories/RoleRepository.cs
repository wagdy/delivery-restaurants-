using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Interfaces;
using RestaurantDelivery.Infrastructure.Data;

namespace RestaurantDelivery.Infrastructure.Repositories;

public class RoleRepository : GenericRepository<Role>, IRoleRepository
{
    public RoleRepository(ApplicationDbContext context) : base(context)
    {
    }

    public Task<List<Role>> GetAllOrderedAsync() =>
        DbSet.OrderBy(r => r.Name).ToListAsync();

    public Task<Role?> GetByNameAsync(string name) =>
        DbSet.FirstOrDefaultAsync(r => r.Name.ToLower() == name.ToLower());

    public Task<int> CountAssignedStaffAsync(int roleId) =>
        Context.Users.CountAsync(u => u.CustomRoleId == roleId);
}
