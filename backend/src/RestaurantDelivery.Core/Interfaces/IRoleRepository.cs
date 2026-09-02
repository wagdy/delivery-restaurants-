using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Core.Interfaces;

public interface IRoleRepository : IGenericRepository<Role>
{
    Task<List<Role>> GetAllOrderedAsync();
    Task<Role?> GetByNameAsync(string name);
    Task<int> CountAssignedStaffAsync(int roleId);
}
