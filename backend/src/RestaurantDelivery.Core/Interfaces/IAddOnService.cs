using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.AddOns;

namespace RestaurantDelivery.Core.Interfaces;

public interface IAddOnService
{
    Task<List<AddOnResponse>> GetAllAsync();
    Task<ServiceResult<AddOnResponse>> CreateAsync(AddOnRequest request);
    Task<ServiceResult<AddOnResponse>> UpdateAsync(int id, AddOnRequest request);
    Task<ServiceResult<bool>> DeleteAsync(int id);
}
