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
