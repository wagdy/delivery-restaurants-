using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Core.Interfaces;

public interface IAddOnRepository : IGenericRepository<AddOn>
{
    Task<List<AddOn>> GetAllOrderedAsync();
    Task<List<AddOn>> GetByIdsAsync(IEnumerable<int> ids);
    Task<AddOn?> GetByNameAsync(string name);
    Task<int> CountOrderUsageAsync(int addOnId);
}
