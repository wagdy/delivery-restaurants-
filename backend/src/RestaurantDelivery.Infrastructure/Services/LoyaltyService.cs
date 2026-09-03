using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Loyalty;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Enums;
using RestaurantDelivery.Core.Interfaces;
using RestaurantDelivery.Infrastructure.Data;

namespace RestaurantDelivery.Infrastructure.Services;

public class LoyaltyService : ILoyaltyService
{
    private const decimal DefaultCurrencyPerPoint = 10m;

    // 100 points = 10 L.E. discount, i.e. DiscountAmount = PointsToRedeem / RedemptionDivisor.
    private const int RedemptionDivisor = 10;

    private readonly ApplicationDbContext _context;
    private readonly decimal _currencyPerPoint;

    public LoyaltyService(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _currencyPerPoint = decimal.TryParse(configuration["Loyalty:CurrencyPerPoint"], out var configured)
            ? configured
            : DefaultCurrencyPerPoint;
    }

    public async Task<LoyaltyMeResponse> GetOrCreateProfileAsync(string appUserId, CancellationToken ct = default)
    {
        var profile = await GetOrCreateProfileEntityAsync(appUserId, ct);
        return MapResponse(profile);
    }

    public async Task<ServiceResult<LoyaltyTransactionResponse>> EarnPointsAsync(string actorId, EarnPointsRequest request, CancellationToken ct = default)
    {
        var customerExists = await _context.Users.AnyAsync(u => u.Id == request.CustomerId, ct);
        if (!customerExists)
        {
            return ServiceResult<LoyaltyTransactionResponse>.Failure("Customer not found.");
        }

        var profile = await GetOrCreateProfileEntityAsync(request.CustomerId, ct);

        var pointsEarned = (int)Math.Floor(request.CheckAmount / _currencyPerPoint);
        if (pointsEarned <= 0)
        {
            return ServiceResult<LoyaltyTransactionResponse>.Failure(
                $"CheckAmount is too small to earn a point at the current {_currencyPerPoint} L.E./point ratio.");
        }

        var previousTier = profile.MembershipTier;

        profile.CurrentPoints += pointsEarned;
        profile.TotalLifetimePoints += pointsEarned;
        profile.LastActivityDate = DateTime.UtcNow;
        profile.MembershipTier = MembershipTierCalculator.CalculateTier(profile.TotalLifetimePoints, profile.MembershipTier);

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

        return ServiceResult<LoyaltyTransactionResponse>.Success(new LoyaltyTransactionResponse
        {
            CustomerId = profile.AppUserId,
            PointsTransacted = pointsEarned,
            CurrentPoints = profile.CurrentPoints,
            TotalLifetimePoints = profile.TotalLifetimePoints,
            MembershipTier = profile.MembershipTier.ToString(),
            TierUpgraded = profile.MembershipTier != previousTier,
            DiscountAmount = 0m
        });
    }

    public async Task<ServiceResult<LoyaltyTransactionResponse>> RedeemPointsAsync(string actorId, RedeemPointsRequest request, CancellationToken ct = default)
    {
        var customerExists = await _context.Users.AnyAsync(u => u.Id == request.CustomerId, ct);
        if (!customerExists)
        {
            return ServiceResult<LoyaltyTransactionResponse>.Failure("Customer not found.");
        }

        var profile = await GetOrCreateProfileEntityAsync(request.CustomerId, ct);

        if (profile.CurrentPoints < request.PointsToRedeem)
        {
            return ServiceResult<LoyaltyTransactionResponse>.Failure("Customer does not have enough points for this redemption.");
        }

        var discountAmount = request.PointsToRedeem / (decimal)RedemptionDivisor;

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

        return ServiceResult<LoyaltyTransactionResponse>.Success(new LoyaltyTransactionResponse
        {
            CustomerId = profile.AppUserId,
            PointsTransacted = -request.PointsToRedeem,
            CurrentPoints = profile.CurrentPoints,
            TotalLifetimePoints = profile.TotalLifetimePoints,
            MembershipTier = profile.MembershipTier.ToString(),
            TierUpgraded = false,
            DiscountAmount = discountAmount
        });
    }

    public async Task<OrderLoyaltyResult> ProcessOrderDeliveredAsync(Order order, CancellationToken ct = default)
    {
        var result = new OrderLoyaltyResult();

        if (order.UserId is null)
        {
            // Guest checkout - no account to credit. Punch campaigns also require an
            // account, so there's nothing else to do here either.
            return result;
        }

        var profile = await GetOrCreateProfileEntityAsync(order.UserId, ct);

        var pointsEarned = (int)Math.Floor(order.TotalAmount / _currencyPerPoint);
        if (pointsEarned > 0)
        {
            var previousTier = profile.MembershipTier;

            profile.CurrentPoints += pointsEarned;
            profile.TotalLifetimePoints += pointsEarned;
            profile.LastActivityDate = DateTime.UtcNow;
            profile.MembershipTier = MembershipTierCalculator.CalculateTier(profile.TotalLifetimePoints, profile.MembershipTier);

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
        }

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A unique-index collision here means this exact order already applied its
            // loyalty side effects (see the unique indexes on LoyaltyPointTransaction.OrderId
            // and LoyaltyPunchTransaction (OrderId, ProgressId)) - most likely a duplicate
            // "mark Delivered" call racing this one. Report nothing new rather than
            // double-awarding or re-sending a WhatsApp confirmation.
            return new OrderLoyaltyResult();
        }

        return result;
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

    private static LoyaltyMeResponse MapResponse(LoyaltyProfile profile) => new()
    {
        AppUserId = profile.AppUserId,
        CurrentPoints = profile.CurrentPoints,
        TotalLifetimePoints = profile.TotalLifetimePoints,
        MembershipTier = profile.MembershipTier.ToString(),
        ReferralCode = profile.ReferralCode
        // AppleWalletAvailable / GoogleWalletAvailable are filled in by the controller,
        // which has access to the wallet settings (an Api-project concern).
    };
}
