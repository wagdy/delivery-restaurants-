using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Interfaces;
using RestaurantDelivery.Infrastructure.Data;

namespace RestaurantDelivery.Infrastructure.Repositories;

public class TierRepository : GenericRepository<LoyaltyTier>, ITierRepository
{
    public TierRepository(ApplicationDbContext context) : base(context)
    {
    }

    public Task<List<LoyaltyTier>> GetAllOrderedAsync() =>
        DbSet.OrderBy(t => t.MinPoints).ToListAsync();

    // Highest-MinPoints match wins in the (should-never-happen-if-validated) case of
    // overlapping ranges, so a customer always resolves to exactly one tier rather than
    // the query throwing on more than one match.
    public Task<LoyaltyTier?> FindByPointsAsync(int totalLifetimePoints) =>
        DbSet
            .Where(t => t.MinPoints <= totalLifetimePoints && (t.MaxPoints == null || totalLifetimePoints <= t.MaxPoints))
            .OrderByDescending(t => t.MinPoints)
            .FirstOrDefaultAsync();

    public async Task<bool> HasOverlappingRangeAsync(int minPoints, int? maxPoints, int? excludeId = null)
    {
        // Two [min, max] ranges (max == null meaning "no ceiling") overlap when each
        // range's start falls at or before the other range's end.
        var effectiveMax = maxPoints ?? int.MaxValue;

        var query = DbSet.Where(t =>
            t.MinPoints <= effectiveMax &&
            minPoints <= (t.MaxPoints ?? int.MaxValue));

        if (excludeId is int id)
        {
            query = query.Where(t => t.Id != id);
        }

        return await query.AnyAsync();
    }
}
