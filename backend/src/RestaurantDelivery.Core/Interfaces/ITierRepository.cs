using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Core.Interfaces;

public interface ITierRepository : IGenericRepository<LoyaltyTier>
{
    Task<List<LoyaltyTier>> GetAllOrderedAsync();

    // Used both to resolve a customer's current tier by points (LoyaltyService) and to
    // reject overlapping ranges on create/update (TierService).
    Task<LoyaltyTier?> FindByPointsAsync(int totalLifetimePoints);

    // excludeId lets an update check for overlap against every *other* tier without
    // always colliding with itself.
    Task<bool> HasOverlappingRangeAsync(int minPoints, int? maxPoints, int? excludeId = null);
}
