using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Checkout;

namespace RestaurantDelivery.Core.Interfaces;

public interface ICheckoutService
{
    Task<ServiceResult<ValidatePromoResponse>> ValidatePromoAsync(ValidatePromoRequest request);
}
