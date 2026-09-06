using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Tiers;

namespace RestaurantDelivery.Core.Interfaces;

public interface ITierService
{
    Task<List<TierResponse>> GetAllAsync();
    Task<ServiceResult<TierResponse>> CreateAsync(TierRequest request);
    Task<ServiceResult<TierResponse>> UpdateAsync(int id, TierRequest request);
    Task<ServiceResult<bool>> DeleteAsync(int id);
}
