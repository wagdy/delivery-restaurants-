using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Loyalty;
using RestaurantDelivery.Core.Entities;

namespace RestaurantDelivery.Core.Interfaces;

public interface ILoyaltyService
{
    Task<LoyaltyMeResponse> GetOrCreateProfileAsync(string appUserId, CancellationToken ct = default);

    // actorId is the authenticated staff member (Module.Scanner) recorded on the transaction.
    Task<ServiceResult<LoyaltyTransactionResponse>> EarnPointsAsync(string actorId, EarnPointsRequest request, CancellationToken ct = default);

    Task<ServiceResult<LoyaltyTransactionResponse>> RedeemPointsAsync(string actorId, RedeemPointsRequest request, CancellationToken ct = default);

    // Called once from AuthService.RegisterAsync, right after a new customer account is
    // created - a flat, system-awarded bonus (no staff actor, no CheckAmount), distinct
    // from EarnPointsAsync's ratio-based staff-scanned earn.
    Task AwardWelcomeBonusAsync(string customerId, CancellationToken ct = default);

    // Called only when reactivating a previously soft-deleted customer (see
    // AuthService.FindOrCreateCustomerByPhoneAsync) - resets CurrentPoints/TotalLifetimePoints
    // to exactly the welcome bonus amount rather than adding to it, since a reactivated
    // customer is explicitly treated as a brand-new welcome, not a returning one with a
    // carried-over balance. Never touches past LoyaltyPointTransaction/LoyaltyPunchTransaction
    // rows - those stay exactly as they were, same as every other soft-delete guarantee here.
    Task ResetToWelcomeBonusAsync(string customerId, CancellationToken ct = default);

    // Called once from OrderService.UpdateStatusAsync when an order first transitions to
    // Delivered. No-op (PointsEarned = 0) if order.UserId is null (guest checkout).
    Task<OrderLoyaltyResult> ProcessOrderDeliveredAsync(Order order, CancellationToken ct = default);

    // Global earn/redeem ratios, editable from the Campaign Manager admin screen.
    Task<LoyaltySettingsResponse> GetSettingsAsync(CancellationToken ct = default);

    Task<ServiceResult<LoyaltySettingsResponse>> UpdateSettingsAsync(UpdateLoyaltySettingsRequest request, CancellationToken ct = default);
}
