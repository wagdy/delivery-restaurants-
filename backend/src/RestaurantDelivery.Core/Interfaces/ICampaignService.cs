using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Loyalty;

namespace RestaurantDelivery.Core.Interfaces;

public interface ICampaignService
{
    Task<List<CampaignResponse>> GetAllAsync(CancellationToken ct = default);

    Task<ServiceResult<CampaignResponse>> CreateAsync(CreateCampaignRequest request, CancellationToken ct = default);

    Task<ServiceResult<CampaignResponse>> ToggleStatusAsync(Guid id, CancellationToken ct = default);

    // Blocked (Failure) if any customer has ever engaged with this campaign, to protect
    // punch/audit history - mirrors RoleService's own delete guard.
    Task<ServiceResult<bool>> DeleteAsync(Guid id, CancellationToken ct = default);

    // Active campaigns only, left-joined with the customer's own progress (0/0 if never punched).
    Task<List<CustomerCampaignProgressResponse>> GetMyProgressAsync(string appUserId, CancellationToken ct = default);

    // actorId is the authenticated staff member (Module.Scanner) recorded on the transaction.
    Task<ServiceResult<PunchResult>> PunchAsync(string actorId, PunchRequest request, CancellationToken ct = default);

    Task<ServiceResult<RedeemRewardResult>> RedeemRewardAsync(string actorId, RedeemRewardRequest request, CancellationToken ct = default);
}
