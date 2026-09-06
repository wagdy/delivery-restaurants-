using System.Text.Json;
using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.PromoCodes;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Infrastructure.Services;

public class PromoCodeService : IPromoCodeService
{
    private readonly IPromoCodeRepository _repository;

    public PromoCodeService(IPromoCodeRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<PromoCodeResponse>> GetAllAsync()
    {
        var codes = await _repository.GetAllOrderedAsync();
        return codes.Select(MapResponse).ToList();
    }

    public async Task<ServiceResult<PromoCodeResponse>> CreateAsync(PromoCodeRequest request)
    {
        var codeText = NormalizeCode(request.CodeText);

        if (await _repository.HasDuplicateCodeAsync(codeText))
        {
            return ServiceResult<PromoCodeResponse>.Failure($"A promo code named \"{codeText}\" already exists.");
        }

        var promoCode = new PromoCode
        {
            CodeText = codeText,
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            TargetIds = SerializeTargetIds(request),
            // Postgres's "timestamp with time zone" column rejects Kind=Unspecified (what
            // a plain "yyyy-MM-dd" JSON date binds to) - same fix as CampaignService's own
            // EndDate handling. ExpiryDate has no meaningful time-of-day, it's just a
            // calendar cutoff, so UTC is as good a fixed Kind as any.
            ExpiryDate = DateTime.SpecifyKind(request.ExpiryDate, DateTimeKind.Utc),
            IsActive = request.IsActive
        };

        await _repository.AddAsync(promoCode);
        await _repository.SaveChangesAsync();

        return ServiceResult<PromoCodeResponse>.Success(MapResponse(promoCode));
    }

    public async Task<ServiceResult<PromoCodeResponse>> UpdateAsync(int id, PromoCodeRequest request)
    {
        var promoCode = await _repository.GetByIdAsync(id);
        if (promoCode is null)
        {
            return ServiceResult<PromoCodeResponse>.Failure("Promo code not found.");
        }

        var codeText = NormalizeCode(request.CodeText);

        if (await _repository.HasDuplicateCodeAsync(codeText, excludeId: id))
        {
            return ServiceResult<PromoCodeResponse>.Failure($"A promo code named \"{codeText}\" already exists.");
        }

        promoCode.CodeText = codeText;
        promoCode.DiscountType = request.DiscountType;
        promoCode.DiscountValue = request.DiscountValue;
        promoCode.TargetIds = SerializeTargetIds(request);
        promoCode.ExpiryDate = DateTime.SpecifyKind(request.ExpiryDate, DateTimeKind.Utc);
        promoCode.IsActive = request.IsActive;

        await _repository.SaveChangesAsync();

        return ServiceResult<PromoCodeResponse>.Success(MapResponse(promoCode));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id)
    {
        var promoCode = await _repository.GetByIdAsync(id);
        if (promoCode is null)
        {
            return ServiceResult<bool>.Failure("Promo code not found.");
        }

        // No "in use" guard - a promo code applied to a past order is stored as a plain
        // text snapshot on that Order (see Order.PromoCodeText), not a foreign key, so
        // deleting the code here can never orphan historical order data.
        _repository.Remove(promoCode);
        await _repository.SaveChangesAsync();

        return ServiceResult<bool>.Success(true);
    }

    private static string NormalizeCode(string codeText) => codeText.Trim().ToUpperInvariant();

    // Only Percentage/DeliveryDiscount are cart-wide, so TargetIds is intentionally
    // dropped for them rather than persisting stale category/item selections a prior
    // edit may have left behind.
    private static string? SerializeTargetIds(PromoCodeRequest request) =>
        request.DiscountType is Core.Enums.PromoDiscountType.SpecificCategory or Core.Enums.PromoDiscountType.SpecificItem
            && request.TargetIds is { Count: > 0 }
            ? JsonSerializer.Serialize(request.TargetIds)
            : null;

    private static PromoCodeResponse MapResponse(PromoCode promoCode) => new()
    {
        Id = promoCode.Id,
        CodeText = promoCode.CodeText,
        DiscountType = promoCode.DiscountType,
        DiscountValue = promoCode.DiscountValue,
        TargetIds = promoCode.TargetIds is null ? null : JsonSerializer.Deserialize<List<int>>(promoCode.TargetIds),
        ExpiryDate = promoCode.ExpiryDate,
        IsActive = promoCode.IsActive,
        IsExpired = promoCode.ExpiryDate <= DateTime.UtcNow
    };
}
