using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Loyalty;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Enums;
using RestaurantDelivery.Core.Interfaces;
using RestaurantDelivery.Infrastructure.Data;

namespace RestaurantDelivery.Infrastructure.Services;

public class LoyaltyService : ILoyaltyService
{
    // Flat signup bonus - see AwardWelcomeBonusAsync. Not part of LoyaltySettings (the
    // admin-editable earn/redeem ratios) since this is a one-time fixed amount, not a
    // rate applied to a currency figure.
    private const int WelcomeBonusPoints = 100;

    private readonly ApplicationDbContext _context;
    private readonly ILoyaltyRealtimeNotifier _realtimeNotifier;
    private readonly ITierRepository _tierRepository;
    private readonly IWhatsAppNotificationService _whatsAppNotificationService;

    public LoyaltyService(
        ApplicationDbContext context,
        ILoyaltyRealtimeNotifier realtimeNotifier,
        ITierRepository tierRepository,
        IWhatsAppNotificationService whatsAppNotificationService)
    {
        _context = context;
        _realtimeNotifier = realtimeNotifier;
        _tierRepository = tierRepository;
        _whatsAppNotificationService = whatsAppNotificationService;
    }

    public async Task<LoyaltyMeResponse> GetOrCreateProfileAsync(string appUserId, CancellationToken ct = default)
    {
        var profile = await GetOrCreateProfileEntityAsync(appUserId, ct);
        return MapResponse(profile);
    }

    public async Task<ServiceResult<LoyaltyTransactionResponse>> EarnPointsAsync(string actorId, EarnPointsRequest request, CancellationToken ct = default)
    {
        var customer = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.CustomerId, ct);
        if (customer is null)
        {
            return ServiceResult<LoyaltyTransactionResponse>.Failure("Customer not found.");
        }

        var profile = await GetOrCreateProfileEntityAsync(request.CustomerId, ct);
        var settings = await GetOrCreateSettingsEntityAsync(ct);

        var pointsEarned = (int)Math.Floor(request.CheckAmount * settings.PointsPerCurrencyUnit);
        if (pointsEarned <= 0)
        {
            return ServiceResult<LoyaltyTransactionResponse>.Failure(
                $"CheckAmount is too small to earn a point at the current {settings.PointsPerCurrencyUnit} points/L.E. ratio.");
        }

        var previousTier = profile.MembershipTier;

        profile.CurrentPoints += pointsEarned;
        profile.TotalLifetimePoints += pointsEarned;
        profile.LastActivityDate = DateTime.UtcNow;
        profile.MembershipTier = await ResolveTierNameAsync(profile.TotalLifetimePoints, ct);

        _context.LoyaltyPointTransactions.Add(new LoyaltyPointTransaction
        {
            CustomerId = profile.AppUserId,
            AdminId = actorId,
            PointsTransacted = pointsEarned,
            CheckAmount = request.CheckAmount,
            TransactionType = LoyaltyTransactionType.Earned,
            CheckReference = request.CheckReference
        });

        await _context.SaveChangesAsync(ct);

        var tierUpgraded = profile.MembershipTier != previousTier;

        await _realtimeNotifier.NotifyPointsUpdatedAsync(profile.AppUserId, new PointsUpdatedPayload
        {
            CurrentPoints = profile.CurrentPoints,
            TotalLifetimePoints = profile.TotalLifetimePoints,
            MembershipTier = profile.MembershipTier ?? "Unranked",
            TierUpgraded = tierUpgraded
        }, ct);

        // Fire-and-forget: never awaited, so this adds zero latency to the cashier's POS
        // response. Not Task.Run either - SendMessageAsync is already I/O-bound (an async
        // HttpClient call), so wrapping it in Task.Run would just burn a threadpool thread
        // blocking on that same I/O instead of freeing it, with no benefit over awaiting a
        // truly async Task without blocking the caller.
        if (!string.IsNullOrWhiteSpace(customer.PhoneNumber))
        {
            _ = _whatsAppNotificationService.SendLoyaltyWalletUpdateAsync(
                customer.PhoneNumber, customer.FullName, isRedemption: false, pointsEarned, profile.CurrentPoints);
        }

        return ServiceResult<LoyaltyTransactionResponse>.Success(new LoyaltyTransactionResponse
        {
            CustomerId = profile.AppUserId,
            PointsTransacted = pointsEarned,
            CurrentPoints = profile.CurrentPoints,
            TotalLifetimePoints = profile.TotalLifetimePoints,
            MembershipTier = profile.MembershipTier ?? "Unranked",
            TierUpgraded = tierUpgraded,
            DiscountAmount = 0m
        });
    }

    public async Task AwardWelcomeBonusAsync(string customerId, CancellationToken ct = default)
    {
        // GetOrCreateProfileEntityAsync always returns a brand-new, just-inserted profile
        // here in practice (called once from AuthService.RegisterAsync right after the
        // account itself is created) but reuses the race-safe helper anyway rather than
        // assuming that invariant.
        var profile = await GetOrCreateProfileEntityAsync(customerId, ct);

        profile.CurrentPoints += WelcomeBonusPoints;
        profile.TotalLifetimePoints += WelcomeBonusPoints;
        profile.LastActivityDate = DateTime.UtcNow;
        profile.MembershipTier = await ResolveTierNameAsync(profile.TotalLifetimePoints, ct);

        _context.LoyaltyPointTransactions.Add(new LoyaltyPointTransaction
        {
            CustomerId = profile.AppUserId,
            PointsTransacted = WelcomeBonusPoints,
            TransactionType = LoyaltyTransactionType.WelcomeBonus
        });

        await _context.SaveChangesAsync(ct);
    }

    public async Task ResetToWelcomeBonusAsync(string customerId, CancellationToken ct = default)
    {
        // A reactivated customer's profile already exists (it was never deleted, only the
        // AppUser row was hidden) - GetOrCreateProfileEntityAsync fetches that same row
        // rather than creating a second one.
        var profile = await GetOrCreateProfileEntityAsync(customerId, ct);

        profile.CurrentPoints = WelcomeBonusPoints;
        profile.TotalLifetimePoints = WelcomeBonusPoints;
        profile.LastActivityDate = DateTime.UtcNow;
        profile.MembershipTier = await ResolveTierNameAsync(profile.TotalLifetimePoints, ct);

        _context.LoyaltyPointTransactions.Add(new LoyaltyPointTransaction
        {
            CustomerId = profile.AppUserId,
            PointsTransacted = WelcomeBonusPoints,
            TransactionType = LoyaltyTransactionType.WelcomeBonus
        });

        await _context.SaveChangesAsync(ct);
    }

    public async Task<ServiceResult<LoyaltyTransactionResponse>> RedeemPointsAsync(string actorId, RedeemPointsRequest request, CancellationToken ct = default)
    {
        var customer = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.CustomerId, ct);
        if (customer is null)
        {
            return ServiceResult<LoyaltyTransactionResponse>.Failure("Customer not found.");
        }

        var profile = await GetOrCreateProfileEntityAsync(request.CustomerId, ct);

        if (profile.CurrentPoints < request.PointsToRedeem)
        {
            return ServiceResult<LoyaltyTransactionResponse>.Failure("Customer does not have enough points for this redemption.");
        }

        var settings = await GetOrCreateSettingsEntityAsync(ct);
        var discountAmount = (request.PointsToRedeem / 100m) * settings.RedemptionValuePer100Points;

        profile.CurrentPoints -= request.PointsToRedeem;
        profile.LastActivityDate = DateTime.UtcNow;

        _context.LoyaltyPointTransactions.Add(new LoyaltyPointTransaction
        {
            CustomerId = profile.AppUserId,
            AdminId = actorId,
            PointsTransacted = -request.PointsToRedeem,
            TransactionType = LoyaltyTransactionType.Redeemed,
            CheckReference = request.CheckReference
        });

        await _context.SaveChangesAsync(ct);

        await _realtimeNotifier.NotifyPointsUpdatedAsync(profile.AppUserId, new PointsUpdatedPayload
        {
            CurrentPoints = profile.CurrentPoints,
            TotalLifetimePoints = profile.TotalLifetimePoints,
            MembershipTier = profile.MembershipTier ?? "Unranked",
            TierUpgraded = false
        }, ct);

        // Fire-and-forget - see the matching comment in EarnPointsAsync above for why this
        // isn't Task.Run. request.PointsToRedeem (not the negated ledger value just stored
        // above) is passed as the positive magnitude - the "🔻 تم استبدال" template already
        // conveys the direction, so a signed number here would double up as "-50".
        if (!string.IsNullOrWhiteSpace(customer.PhoneNumber))
        {
            _ = _whatsAppNotificationService.SendLoyaltyWalletUpdateAsync(
                customer.PhoneNumber, customer.FullName, isRedemption: true, request.PointsToRedeem, profile.CurrentPoints);
        }

        return ServiceResult<LoyaltyTransactionResponse>.Success(new LoyaltyTransactionResponse
        {
            CustomerId = profile.AppUserId,
            PointsTransacted = -request.PointsToRedeem,
            CurrentPoints = profile.CurrentPoints,
            TotalLifetimePoints = profile.TotalLifetimePoints,
            MembershipTier = profile.MembershipTier ?? "Unranked",
            TierUpgraded = false,
            DiscountAmount = discountAmount
        });
    }

    public async Task<OrderLoyaltyResult> ProcessOrderDeliveredAsync(Order order, CancellationToken ct = default)
    {
        var result = new OrderLoyaltyResult();

        // Re-checked here (OrderService.UpdateStatusAsync already checks this once before
        // calling in) as a defense against two concurrent requests both reading the flag
        // as false before either commits. Set immediately, in the same tracked-entity
        // change set as everything below, so it's part of the one SaveChangesAsync at the
        // end - if that save fails for any reason, PointsAwarded is never persisted as
        // true either, so a genuine failure can still be retried by a later status change.
        if (order.PointsAwarded)
        {
            return result;
        }

        order.PointsAwarded = true;

        if (order.UserId is null)
        {
            // Guest checkout - no account to credit. Punch campaigns also require an
            // account, so there's nothing else to do here either - just persist the flag
            // above so a later toggle doesn't re-attempt this for the same guest order.
            await _context.SaveChangesAsync(ct);
            return result;
        }

        var profile = await GetOrCreateProfileEntityAsync(order.UserId, ct);
        var settings = await GetOrCreateSettingsEntityAsync(ct);

        // Dynamic ratio (admin-editable via GET/PUT api/loyalty/settings) instead of the
        // old hardcoded currency-per-point constant.
        var pointsEarned = (int)Math.Floor(order.TotalAmount * settings.PointsPerCurrencyUnit);
        if (pointsEarned > 0)
        {
            var previousTier = profile.MembershipTier;

            profile.CurrentPoints += pointsEarned;
            profile.TotalLifetimePoints += pointsEarned;
            profile.LastActivityDate = DateTime.UtcNow;
            profile.MembershipTier = await ResolveTierNameAsync(profile.TotalLifetimePoints, ct);

            _context.LoyaltyPointTransactions.Add(new LoyaltyPointTransaction
            {
                CustomerId = profile.AppUserId,
                PointsTransacted = pointsEarned,
                CheckAmount = order.TotalAmount,
                TransactionType = LoyaltyTransactionType.OrderEarned,
                OrderId = order.Id
            });

            result.PointsEarned = pointsEarned;
            result.TierUpgraded = profile.MembershipTier != previousTier;
        }

        result.NewTotalPoints = profile.CurrentPoints;

        // Collected during the loop below and only sent after SaveChangesAsync succeeds -
        // never fire a "your balance changed" push for a change that got rolled back.
        var pendingPunchNotifications = new List<PunchUpdatedPayload>();

        var activeCampaigns = await _context.LoyaltyCampaigns.Where(c => c.IsActive).ToListAsync(ct);
        foreach (var campaign in activeCampaigns)
        {
            var matchingQuantity = order.OrderItems
                .Where(oi => campaign.CategoryName is null ||
                             string.Equals(oi.MenuItem.Category, campaign.CategoryName, StringComparison.OrdinalIgnoreCase))
                .Sum(oi => oi.Quantity);

            if (matchingQuantity <= 0)
            {
                continue;
            }

            var progress = await GetOrCreateCampaignProgressAsync(profile.AppUserId, campaign.Id, ct);

            var totalPunches = progress.CurrentPunches + matchingQuantity;
            var rewardsToAdd = totalPunches / campaign.TargetPunches;
            progress.CurrentPunches = totalPunches % campaign.TargetPunches;
            progress.RewardsEarned += rewardsToAdd;
            progress.LastPunchDate = DateTime.UtcNow;

            _context.LoyaltyPunchTransactions.Add(new LoyaltyPunchTransaction
            {
                ProgressId = progress.Id,
                TransactionType = PunchTransactionType.PunchAdded,
                Quantity = matchingQuantity,
                OrderId = order.Id
            });

            result.PunchUpdates.Add(new PunchUpdateSummary
            {
                CampaignTitle = campaign.Title,
                QuantityApplied = matchingQuantity,
                CurrentPunches = progress.CurrentPunches,
                TargetPunches = campaign.TargetPunches,
                RewardsEarnedThisOrder = rewardsToAdd
            });

            pendingPunchNotifications.Add(new PunchUpdatedPayload
            {
                CampaignId = campaign.Id,
                CampaignTitle = campaign.Title,
                CurrentPunches = progress.CurrentPunches,
                TargetPunches = campaign.TargetPunches,
                RewardsEarned = progress.RewardsEarned,
                RewardEarnedThisPunch = rewardsToAdd > 0
            });
        }

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Backstop for the race the PointsAwarded check above can't catch alone: two
            // concurrent requests both reading PointsAwarded=false before either commits.
            // The unique indexes on LoyaltyPointTransaction.OrderId and
            // LoyaltyPunchTransaction (OrderId, ProgressId) catch the actual duplicate
            // write. Report nothing new rather than double-awarding or re-sending a
            // WhatsApp confirmation.
            return new OrderLoyaltyResult();
        }

        if (pointsEarned > 0)
        {
            await _realtimeNotifier.NotifyPointsUpdatedAsync(profile.AppUserId, new PointsUpdatedPayload
            {
                CurrentPoints = profile.CurrentPoints,
                TotalLifetimePoints = profile.TotalLifetimePoints,
                MembershipTier = profile.MembershipTier ?? "Unranked",
                TierUpgraded = result.TierUpgraded
            }, ct);
        }

        foreach (var payload in pendingPunchNotifications)
        {
            await _realtimeNotifier.NotifyPunchUpdatedAsync(profile.AppUserId, payload, ct);
        }

        return result;
    }

    public async Task<LoyaltySettingsResponse> GetSettingsAsync(CancellationToken ct = default)
    {
        var settings = await GetOrCreateSettingsEntityAsync(ct);
        return MapSettingsResponse(settings);
    }

    public async Task<ServiceResult<LoyaltySettingsResponse>> UpdateSettingsAsync(UpdateLoyaltySettingsRequest request, CancellationToken ct = default)
    {
        var settings = await GetOrCreateSettingsEntityAsync(ct);

        settings.PointsPerCurrencyUnit = request.PointsPerCurrencyUnit;
        settings.RedemptionValuePer100Points = request.RedemptionValuePer100Points;

        await _context.SaveChangesAsync(ct);

        return ServiceResult<LoyaltySettingsResponse>.Success(MapSettingsResponse(settings));
    }

    // Replaces the old hardcoded MembershipTierCalculator.CalculateTier - dynamically
    // queries the admin-configurable LoyaltyTiers table instead of a fixed enum. Callers
    // never need to pass (or compare against) the customer's *current* tier the way the
    // old calculator's "never downgrade" logic did: TotalLifetimePoints only ever
    // increases (RedeemPointsAsync deducts CurrentPoints, never TotalLifetimePoints), so
    // recomputing fresh from TotalLifetimePoints on every call is already monotonically
    // upgrade-only by construction. Returns null if no configured tier's range covers
    // this point total (including "no tiers configured yet").
    private async Task<string?> ResolveTierNameAsync(int totalLifetimePoints, CancellationToken ct)
    {
        var tier = await _tierRepository.FindByPointsAsync(totalLifetimePoints);
        return tier?.Name;
    }

    // Same race-safe get-or-create pattern as GetOrCreateProfileEntityAsync below.
    private async Task<LoyaltyCampaignProgress> GetOrCreateCampaignProgressAsync(string customerId, Guid campaignId, CancellationToken ct)
    {
        var progress = await _context.LoyaltyCampaignProgress
            .FirstOrDefaultAsync(p => p.CustomerId == customerId && p.CampaignId == campaignId, ct);
        if (progress is not null)
        {
            return progress;
        }

        progress = new LoyaltyCampaignProgress { CustomerId = customerId, CampaignId = campaignId };
        _context.LoyaltyCampaignProgress.Add(progress);
        return progress;
    }

    // Lazily creates the profile row on first touch. Two concurrent first-touches (e.g. a
    // GET /me and a wallet pass request landing at the same time) would both try to insert
    // the same PK - the loser's SaveChanges throws a unique-violation, so it's caught here
    // and re-queried rather than bubbling a 500.
    private async Task<LoyaltyProfile> GetOrCreateProfileEntityAsync(string appUserId, CancellationToken ct)
    {
        var profile = await _context.LoyaltyProfiles.FirstOrDefaultAsync(p => p.AppUserId == appUserId, ct);
        if (profile is not null)
        {
            return profile;
        }

        profile = new LoyaltyProfile { AppUserId = appUserId };
        _context.LoyaltyProfiles.Add(profile);

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            _context.Entry(profile).State = EntityState.Detached;
            profile = await _context.LoyaltyProfiles.FirstAsync(p => p.AppUserId == appUserId, ct);
        }

        return profile;
    }

    // Simple get-or-create (no race-safe catch/retry like the profile/progress helpers
    // above) - mirrors SettingsService.GetOrCreateAsync's pattern for the same reason:
    // this row is written rarely (an admin editing a form), not on every checkout, so the
    // odds of a genuine concurrent first-touch are negligible.
    private async Task<LoyaltySettings> GetOrCreateSettingsEntityAsync(CancellationToken ct)
    {
        var settings = await _context.LoyaltySettings.FirstOrDefaultAsync(ct);
        if (settings is not null)
        {
            return settings;
        }

        settings = new LoyaltySettings();
        _context.LoyaltySettings.Add(settings);
        await _context.SaveChangesAsync(ct);
        return settings;
    }

    private static LoyaltySettingsResponse MapSettingsResponse(LoyaltySettings settings) => new()
    {
        PointsPerCurrencyUnit = settings.PointsPerCurrencyUnit,
        RedemptionValuePer100Points = settings.RedemptionValuePer100Points
    };

    private static LoyaltyMeResponse MapResponse(LoyaltyProfile profile) => new()
    {
        AppUserId = profile.AppUserId,
        CurrentPoints = profile.CurrentPoints,
        TotalLifetimePoints = profile.TotalLifetimePoints,
        MembershipTier = profile.MembershipTier ?? "Unranked",
        ReferralCode = profile.ReferralCode
        // AppleWalletAvailable / GoogleWalletAvailable are filled in by the controller,
        // which has access to the wallet settings (an Api-project concern).
    };
}
