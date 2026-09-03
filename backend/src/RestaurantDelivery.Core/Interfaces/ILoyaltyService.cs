using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Loyalty;

namespace RestaurantDelivery.Core.Interfaces;

public interface ILoyaltyService
{
    Task<LoyaltyMeResponse> GetOrCreateProfileAsync(string appUserId, CancellationToken ct = default);

    // actorId is the authenticated staff member (Module.Customers) recorded on the transaction.
    Task<ServiceResult<LoyaltyTransactionResponse>> EarnPointsAsync(string actorId, EarnPointsRequest request, CancellationToken ct = default);

    Task<ServiceResult<LoyaltyTransactionResponse>> RedeemPointsAsync(string actorId, RedeemPointsRequest request, CancellationToken ct = default);
}
