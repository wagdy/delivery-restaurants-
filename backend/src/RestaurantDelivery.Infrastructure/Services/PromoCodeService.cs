using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.PromoCodes;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Enums;
using RestaurantDelivery.Core.Interfaces;
using RestaurantDelivery.Infrastructure.Data;

namespace RestaurantDelivery.Infrastructure.Services;

public class PromoCodeService : IPromoCodeService
{
    private readonly IPromoCodeRepository _repository;
    private readonly ApplicationDbContext _context;
    private readonly IWhatsAppBroadcastQueue _broadcastQueue;

    public PromoCodeService(IPromoCodeRepository repository, ApplicationDbContext context, IWhatsAppBroadcastQueue broadcastQueue)
    {
        _repository = repository;
        _context = context;
        _broadcastQueue = broadcastQueue;
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

        if (request.NotifyCustomersViaWhatsApp)
        {
            await EnqueueBroadcastAsync(promoCode);
        }

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

        if (request.NotifyCustomersViaWhatsApp)
        {
            await EnqueueBroadcastAsync(promoCode);
        }

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

    // Fetched synchronously (a fast, single indexed query) at the moment the promo code is
    // saved - only the actual SENDING loop (with its multi-second anti-ban delay per
    // customer) is deferred to the background queue, per the explicit requirement that the
    // HTTP response itself never blocks on that.
    private async Task EnqueueBroadcastAsync(PromoCode promoCode)
    {
        var phoneNumbers = await _context.Users
            .Where(u => u.Role == UserRole.Customer && !u.IsDeleted && !string.IsNullOrWhiteSpace(u.PhoneNumber))
            .Select(u => u.PhoneNumber!)
            .ToListAsync();

        if (phoneNumbers.Count == 0)
        {
            return;
        }

        _broadcastQueue.Enqueue(new WhatsAppBroadcastJob
        {
            PhoneNumbers = phoneNumbers,
            Message = BuildBroadcastMessage(promoCode)
        });
    }

    // No trailing "شوف المنيو" link here, unlike the exact template originally specified -
    // SendBroadcastMessageAsync's underlying SendMessageAsync already appends that exact
    // line (PromotionalFooter) to every outgoing message, so repeating it here would show
    // it twice. Same fix already applied to SendPastCustomerWelcomeAsync's own template
    // for the same reason.
    private static string BuildBroadcastMessage(PromoCode promoCode) =>
        $"مفاجأة من أوتانتيك! 🎉\n\n" +
        $"استخدم كود الخصم: {promoCode.CodeText}\n" +
        $"واستمتع بـ {DescribeDiscount(promoCode)} على طلبك القادم!\n" +
        $"(الكود صالح حتى {promoCode.ExpiryDate:yyyy-MM-dd})";

    // PromoCode has no standalone "DiscountDetails" field - DiscountValue is always a
    // percentage, its meaning depending on DiscountType (see that enum's own doc comment) -
    // so this derives the same short Arabic phrase the message template's own placeholder
    // implied, one branch per type.
    private static string DescribeDiscount(PromoCode promoCode) => promoCode.DiscountType switch
    {
        PromoDiscountType.DeliveryDiscount when promoCode.DiscountValue >= 100 => "توصيل مجاني",
        PromoDiscountType.DeliveryDiscount => $"خصم {promoCode.DiscountValue:0.##}% على التوصيل",
        PromoDiscountType.SpecificCategory or PromoDiscountType.SpecificItem =>
            $"خصم {promoCode.DiscountValue:0.##}% على أصناف مختارة",
        _ => $"خصم {promoCode.DiscountValue:0.##}%"
    };

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
