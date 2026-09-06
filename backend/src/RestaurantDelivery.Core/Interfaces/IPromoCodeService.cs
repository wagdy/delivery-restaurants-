using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.PromoCodes;

namespace RestaurantDelivery.Core.Interfaces;

public interface IPromoCodeService
{
    Task<List<PromoCodeResponse>> GetAllAsync();
    Task<ServiceResult<PromoCodeResponse>> CreateAsync(PromoCodeRequest request);
    Task<ServiceResult<PromoCodeResponse>> UpdateAsync(int id, PromoCodeRequest request);
    Task<ServiceResult<bool>> DeleteAsync(int id);
}
