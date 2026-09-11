using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Loyalty;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Enums;
using RestaurantDelivery.Core.Interfaces;
using RestaurantDelivery.Infrastructure.Data;

namespace RestaurantDelivery.Infrastructure.Services;

public class CampaignService : ICampaignService
{
    private readonly ApplicationDbContext _context;
    private readonly ILoyaltyRealtimeNotifier _realtimeNotifier;
    private readonly IWhatsAppBroadcastQueue _broadcastQueue;

    public CampaignService(ApplicationDbContext context, ILoyaltyRealtimeNotifier realtimeNotifier, IWhatsAppBroadcastQueue broadcastQueue)
    {
        _context = context;
        _realtimeNotifier = realtimeNotifier;
        _broadcastQueue = broadcastQueue;
    }

    public async Task<List<CampaignResponse>> GetAllAsync(CancellationToken ct = default) =>
        await _context.LoyaltyCampaigns
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => MapResponse(c))
            .ToListAsync(ct);

    public async Task<ServiceResult<CampaignResponse>> CreateAsync(CreateCampaignRequest request, CancellationToken ct = default)
    {
        var campaign = new LoyaltyCampaign
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            CategoryName = string.IsNullOrWhiteSpace(request.CategoryName) ? null : request.CategoryName.Trim(),
            TargetPunches = request.TargetPunches,
            // [Required] on CreateCampaignRequest.EndDate guarantees this is non-null by the
            // time this method runs. System.Text.Json parses "2026-12-25" (the
            // <input type="date"> value) into a DateTime with Kind=Unspecified - Npgsql
            // refuses to write that into a "timestamp with time zone" column ("only UTC is
            // supported"). EndDate has no meaningful time-of-day/timezone here, it's just a
            // calendar boundary, so tagging it UTC (rather than converting) is correct.
            EndDate = DateTime.SpecifyKind(request.EndDate!.Value, DateTimeKind.Utc),
            StartDate = DateTime.UtcNow
        };

        _context.LoyaltyCampaigns.Add(campaign);
        await _context.SaveChangesAsync(ct);

        if (request.NotifyCustomersViaWhatsApp)
        {
            await EnqueueBroadcastAsync(campaign, ct);
        }

        return ServiceResult<CampaignResponse>.Success(MapResponse(campaign));
    }

    // Fetched synchronously (a fast, single indexed query) at the moment the campaign is
    // saved - only the actual SENDING loop (with its multi-second anti-ban delay per
    // customer) is deferred to the background queue, per the explicit requirement that the
    // HTTP response itself never blocks on that.
    private async Task EnqueueBroadcastAsync(LoyaltyCampaign campaign, CancellationToken ct)
    {
        var phoneNumbers = await _context.Users
            .Where(u => u.Role == UserRole.Customer && !u.IsDeleted && !string.IsNullOrWhiteSpace(u.PhoneNumber))
            .Select(u => u.PhoneNumber!)
            .ToListAsync(ct);

        if (phoneNumbers.Count == 0)
        {
            return;
        }

        _broadcastQueue.Enqueue(new WhatsAppBroadcastJob
        {
            PhoneNumbers = phoneNumbers,
            Message = BuildBroadcastMessage(campaign)
        });
    }

    // No trailing "شوف المنيو" link here, unlike the exact template originally specified -
    // SendBroadcastMessageAsync's underlying SendMessageAsync already appends that exact
    // line (PromotionalFooter) to every outgoing message, so repeating it here would show
    // it twice. Same fix already applied to SendPastCustomerWelcomeAsync's own template
    // for the same reason. Description doubles as the "reward description" - there's no
    // separate RewardDescription field on LoyaltyCampaign, see that entity's doc comment.
    private static string BuildBroadcastMessage(LoyaltyCampaign campaign) =>
        $"نظام مكافآت جديد من أوتانتيك! 💳\n\n" +
        $"فعلنا كارت '{campaign.Title}'.\n" +
        $"اطلب {campaign.TargetPunches} مرات واكسب {campaign.Description} مجاناً!";

    public async Task<ServiceResult<CampaignResponse>> ToggleStatusAsync(Guid id, CancellationToken ct = default)
    {
        var campaign = await _context.LoyaltyCampaigns.FindAsync([id], ct);
        if (campaign is null)
        {
            return ServiceResult<CampaignResponse>.Failure("Campaign not found.");
        }

        campaign.IsActive = !campaign.IsActive;
        if (campaign.IsActive)
        {
            campaign.StartDate = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);

        return ServiceResult<CampaignResponse>.Success(MapResponse(campaign));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var campaign = await _context.LoyaltyCampaigns.FindAsync([id], ct);
        if (campaign is null)
        {
            return ServiceResult<bool>.Failure("Campaign not found.");
        }

        var hasHistory = await _context.LoyaltyCampaignProgress.AnyAsync(p => p.CampaignId == id, ct);
        if (hasHistory)
        {
            return ServiceResult<bool>.Failure("This campaign has customer history and can't be deleted - deactivate it instead.");
        }

        _context.LoyaltyCampaigns.Remove(campaign);
        await _context.SaveChangesAsync(ct);

        return ServiceResult<bool>.Success(true);
    }

    public async Task<List<CustomerCampaignProgressResponse>> GetMyProgressAsync(string appUserId, CancellationToken ct = default)
    {
        var activeCampaigns = await _context.LoyaltyCampaigns.Where(c => c.IsActive).ToListAsync(ct);
        var progressByCampaign = await _context.LoyaltyCampaignProgress
            .Where(p => p.CustomerId == appUserId)
            .ToDictionaryAsync(p => p.CampaignId, ct);

        return activeCampaigns.Select(c =>
        {
            progressByCampaign.TryGetValue(c.Id, out var progress);
            return new CustomerCampaignProgressResponse
            {
                CampaignId = c.Id,
                CampaignTitle = c.Title,
                CampaignDescription = c.Description,
                TargetPunches = c.TargetPunches,
                CurrentPunches = progress?.CurrentPunches ?? 0,
                RewardsEarned = progress?.RewardsEarned ?? 0,
                EndDate = c.EndDate
            };
        }).ToList();
    }

    public async Task<ServiceResult<PunchResult>> PunchAsync(string actorId, PunchRequest request, CancellationToken ct = default)
    {
        var customerExists = await _context.Users.AnyAsync(u => u.Id == request.CustomerId, ct);
        if (!customerExists)
        {
            return ServiceResult<PunchResult>.Failure("Customer not found.");
        }

        var campaign = await _context.LoyaltyCampaigns.FirstOrDefaultAsync(c => c.Id == request.CampaignId && c.IsActive, ct);
        if (campaign is null)
        {
            return ServiceResult<PunchResult>.Failure("Campaign not found or is not active.");
        }

        var progress = await GetOrCreateProgressAsync(request.CustomerId, campaign.Id, ct);

        if (progress.CurrentPunches >= campaign.TargetPunches)
        {
            return ServiceResult<PunchResult>.Failure(
                "This customer's progress is out of sync with the campaign target - please refresh and try again.");
        }

        progress.CurrentPunches += 1;
        progress.LastPunchDate = DateTime.UtcNow;

        var rewardEarnedThisPunch = false;
        if (progress.CurrentPunches == campaign.TargetPunches)
        {
            progress.CurrentPunches = 0;
            progress.RewardsEarned += 1;
            rewardEarnedThisPunch = true;
        }

        _context.LoyaltyPunchTransactions.Add(new LoyaltyPunchTransaction
        {
            ProgressId = progress.Id,
            AdminId = actorId,
            TransactionType = PunchTransactionType.PunchAdded,
            Quantity = 1,
            CheckReference = request.CheckReference
        });

        await _context.SaveChangesAsync(ct);

        await _realtimeNotifier.NotifyPunchUpdatedAsync(request.CustomerId, new PunchUpdatedPayload
        {
            CampaignId = campaign.Id,
            CampaignTitle = campaign.Title,
            CurrentPunches = progress.CurrentPunches,
            TargetPunches = campaign.TargetPunches,
            RewardsEarned = progress.RewardsEarned,
            RewardEarnedThisPunch = rewardEarnedThisPunch
        }, ct);

        return ServiceResult<PunchResult>.Success(new PunchResult
        {
            CustomerId = request.CustomerId,
            CampaignId = campaign.Id,
            CampaignTitle = campaign.Title,
            CurrentPunches = progress.CurrentPunches,
            TargetPunches = campaign.TargetPunches,
            RewardsEarned = progress.RewardsEarned,
            RewardEarnedThisPunch = rewardEarnedThisPunch
        });
    }

    public async Task<ServiceResult<RedeemRewardResult>> RedeemRewardAsync(string actorId, RedeemRewardRequest request, CancellationToken ct = default)
    {
        // No IsActive filter - an already-earned reward stays redeemable even after the
        // campaign is deactivated.
        var campaign = await _context.LoyaltyCampaigns.FindAsync([request.CampaignId], ct);
        if (campaign is null)
        {
            return ServiceResult<RedeemRewardResult>.Failure("Campaign not found.");
        }

        var progress = await _context.LoyaltyCampaignProgress
            .FirstOrDefaultAsync(p => p.CustomerId == request.CustomerId && p.CampaignId == request.CampaignId, ct);

        if (progress is null || progress.RewardsEarned <= 0)
        {
            return ServiceResult<RedeemRewardResult>.Failure("This customer has no unredeemed rewards for this campaign.");
        }

        progress.RewardsEarned -= 1;

        _context.LoyaltyPunchTransactions.Add(new LoyaltyPunchTransaction
        {
            ProgressId = progress.Id,
            AdminId = actorId,
            TransactionType = PunchTransactionType.RewardClaimed,
            Quantity = 1,
            CheckReference = request.CheckReference
        });

        await _context.SaveChangesAsync(ct);

        await _realtimeNotifier.NotifyPunchUpdatedAsync(request.CustomerId, new PunchUpdatedPayload
        {
            CampaignId = campaign.Id,
            CampaignTitle = campaign.Title,
            CurrentPunches = progress.CurrentPunches,
            TargetPunches = campaign.TargetPunches,
            RewardsEarned = progress.RewardsEarned,
            RewardEarnedThisPunch = false
        }, ct);

        return ServiceResult<RedeemRewardResult>.Success(new RedeemRewardResult
        {
            CustomerId = request.CustomerId,
            CampaignId = campaign.Id,
            CampaignTitle = campaign.Title,
            RewardsEarned = progress.RewardsEarned
        });
    }

    // Same race-safe get-or-create pattern as LoyaltyService.GetOrCreateProfileEntityAsync
    // (also duplicated there for the automatic order-triggered punch path, since the two
    // services don't share a concrete dependency on each other).
    private async Task<LoyaltyCampaignProgress> GetOrCreateProgressAsync(string customerId, Guid campaignId, CancellationToken ct)
    {
        var progress = await _context.LoyaltyCampaignProgress
            .FirstOrDefaultAsync(p => p.CustomerId == customerId && p.CampaignId == campaignId, ct);
        if (progress is not null)
        {
            return progress;
        }

        progress = new LoyaltyCampaignProgress { CustomerId = customerId, CampaignId = campaignId };
        _context.LoyaltyCampaignProgress.Add(progress);

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            _context.Entry(progress).State = EntityState.Detached;
            progress = await _context.LoyaltyCampaignProgress
                .FirstAsync(p => p.CustomerId == customerId && p.CampaignId == campaignId, ct);
        }

        return progress;
    }

    private static CampaignResponse MapResponse(LoyaltyCampaign c) => new()
    {
        Id = c.Id,
        Title = c.Title,
        Description = c.Description,
        CategoryName = c.CategoryName,
        TargetPunches = c.TargetPunches,
        IsActive = c.IsActive,
        StartDate = c.StartDate,
        EndDate = c.EndDate,
        CreatedAt = c.CreatedAt
    };
}
