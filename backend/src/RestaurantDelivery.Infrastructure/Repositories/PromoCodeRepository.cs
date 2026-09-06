using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Interfaces;
using RestaurantDelivery.Infrastructure.Data;

namespace RestaurantDelivery.Infrastructure.Repositories;

public class PromoCodeRepository : GenericRepository<PromoCode>, IPromoCodeRepository
{
    public PromoCodeRepository(ApplicationDbContext context) : base(context)
    {
    }

    public Task<List<PromoCode>> GetAllOrderedAsync() =>
        DbSet.OrderByDescending(p => p.ExpiryDate).ToListAsync();

    public Task<PromoCode?> GetByCodeAsync(string codeText) =>
        DbSet.FirstOrDefaultAsync(p => p.CodeText == codeText);

    public async Task<bool> HasDuplicateCodeAsync(string codeText, int? excludeId = null)
    {
        var query = DbSet.Where(p => p.CodeText == codeText);
        if (excludeId is int id)
        {
            query = query.Where(p => p.Id != id);
        }

        return await query.AnyAsync();
    }
}
