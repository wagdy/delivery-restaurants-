using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Core.Interfaces;

public interface IPromoCodeRepository : IGenericRepository<PromoCode>
{
    Task<List<PromoCode>> GetAllOrderedAsync();

    // Case-insensitive - "SAVE10" and "save10" must resolve to the same code both when
    // an admin creates one and when a customer redeems it. Normalized to uppercase
    // before this is ever called (see PromoCodeService), so a plain equality match
    // against the already-uppercase CodeText column suffices.
    Task<PromoCode?> GetByCodeAsync(string codeText);

    Task<bool> HasDuplicateCodeAsync(string codeText, int? excludeId = null);
}
