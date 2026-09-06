using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Tiers;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Infrastructure.Services;

public class TierService : ITierService
{
    private readonly ITierRepository _repository;

    public TierService(ITierRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<TierResponse>> GetAllAsync()
    {
        var tiers = await _repository.GetAllOrderedAsync();
        return tiers.Select(MapResponse).ToList();
    }

    public async Task<ServiceResult<TierResponse>> CreateAsync(TierRequest request)
    {
        var name = request.Name.Trim();

        if (await _repository.HasOverlappingRangeAsync(request.MinPoints, request.MaxPoints))
        {
            return ServiceResult<TierResponse>.Failure(
                "This point range overlaps with an existing tier. Adjust the range so tiers never overlap.");
        }

        var tier = new LoyaltyTier { Name = name, MinPoints = request.MinPoints, MaxPoints = request.MaxPoints };
        await _repository.AddAsync(tier);
        await _repository.SaveChangesAsync();

        return ServiceResult<TierResponse>.Success(MapResponse(tier));
    }

    public async Task<ServiceResult<TierResponse>> UpdateAsync(int id, TierRequest request)
    {
        var tier = await _repository.GetByIdAsync(id);
        if (tier is null)
        {
            return ServiceResult<TierResponse>.Failure("Tier not found.");
        }

        if (await _repository.HasOverlappingRangeAsync(request.MinPoints, request.MaxPoints, excludeId: id))
        {
            return ServiceResult<TierResponse>.Failure(
                "This point range overlaps with an existing tier. Adjust the range so tiers never overlap.");
        }

        tier.Name = request.Name.Trim();
        tier.MinPoints = request.MinPoints;
        tier.MaxPoints = request.MaxPoints;

        await _repository.SaveChangesAsync();

        return ServiceResult<TierResponse>.Success(MapResponse(tier));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id)
    {
        var tier = await _repository.GetByIdAsync(id);
        if (tier is null)
        {
            return ServiceResult<bool>.Failure("Tier not found.");
        }

        // No "in use" guard like Category/SubCategory's own delete blockers - a customer
        // whose points no longer match any configured tier simply resolves to no tier
        // (LoyaltyService.ResolveTierNameAsync returns null) rather than being an invalid
        // foreign key, since LoyaltyProfile.MembershipTier stores the tier's name as a
        // plain string snapshot, not a reference to this row.
        _repository.Remove(tier);
        await _repository.SaveChangesAsync();

        return ServiceResult<bool>.Success(true);
    }

    private static TierResponse MapResponse(LoyaltyTier tier) => new()
    {
        Id = tier.Id,
        Name = tier.Name,
        MinPoints = tier.MinPoints,
        MaxPoints = tier.MaxPoints
    };
}
