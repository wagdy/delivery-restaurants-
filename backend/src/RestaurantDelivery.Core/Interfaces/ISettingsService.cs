using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Settings;

namespace RestaurantDelivery.Core.Interfaces;

public interface ISettingsService
{
    Task<RestaurantSettingsResponse> GetAsync();
    Task<ServiceResult<RestaurantSettingsResponse>> UpdateAsync(UpdateRestaurantSettingsRequest request);
}
