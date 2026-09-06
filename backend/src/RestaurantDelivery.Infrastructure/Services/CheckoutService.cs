using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Checkout;
using RestaurantDelivery.Core.Enums;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Infrastructure.Services;

public class CheckoutService : ICheckoutService
{
    private readonly IPromoCodeRepository _promoCodeRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ISettingsService _settingsService;

    public CheckoutService(
        IPromoCodeRepository promoCodeRepository,
        IOrderRepository orderRepository,
        ICategoryRepository categoryRepository,
        ISettingsService settingsService)
    {
        _promoCodeRepository = promoCodeRepository;
        _orderRepository = orderRepository;
        _categoryRepository = categoryRepository;
        _settingsService = settingsService;
    }

    public async Task<ServiceResult<ValidatePromoResponse>> ValidatePromoAsync(ValidatePromoRequest request)
    {
        var codeText = request.CodeText.Trim().ToUpperInvariant();
        var promo = await _promoCodeRepository.GetByCodeAsync(codeText);

        if (promo is null)
        {
            return ServiceResult<ValidatePromoResponse>.Failure("This promo code was not found.");
        }

        if (!promo.IsActive)
        {
            return ServiceResult<ValidatePromoResponse>.Failure("This promo code is no longer active.");
        }

        if (promo.ExpiryDate <= DateTime.UtcNow)
        {
            return ServiceResult<ValidatePromoResponse>.Failure("This promo code has expired.");
        }

        var (lines, subtotal) = await ResolveCartLinesAsync(request.Items);

        var categoryNamesById = new Dictionary<int, string>();
        if (promo.DiscountType == PromoDiscountType.SpecificCategory)
        {
            var targetIds = PromoDiscountCalculator.ParseTargetIds(promo.TargetIds);
            var categories = await _categoryRepository.GetByIdsAsync(targetIds);
            categoryNamesById = categories.ToDictionary(c => c.Id, c => c.Name);
        }

        var discount = PromoDiscountCalculator.Calculate(promo, lines, request.DeliveryFee, categoryNamesById);

        var settings = await _settingsService.GetAsync();
        var taxableAmount = subtotal - discount.ItemDiscountAmount;
        var taxAmount = Math.Round(taxableAmount * (settings.TaxPercentage / 100m), 2);
        var deliveryFeeAfterDiscount = request.DeliveryFee - discount.DeliveryDiscountAmount;
        var total = taxableAmount + taxAmount + deliveryFeeAfterDiscount;

        return ServiceResult<ValidatePromoResponse>.Success(new ValidatePromoResponse
        {
            Subtotal = subtotal,
            DiscountAmount = discount.ItemDiscountAmount,
            DeliveryFee = request.DeliveryFee,
            DeliveryDiscountAmount = discount.DeliveryDiscountAmount,
            TaxAmount = taxAmount,
            Total = total,
            AffectedMenuItemIds = discount.AffectedMenuItemIds,
            Description = discount.Description
        });
    }

    // Server-resolved prices only - request.Items carries just MenuItemId/Quantity/AddOnIds,
    // never a price, so a tampered client payload can't manufacture a discount.
    // An unknown MenuItemId is silently skipped rather than failing the whole preview -
    // this endpoint is a best-effort estimate; OrderService.BuildOrderItemsAsync is the
    // strict, authoritative check that actually gates order placement.
    private async Task<(List<PromoDiscountCalculator.CartLine> Lines, decimal Subtotal)> ResolveCartLinesAsync(
        List<Core.DTOs.Checkout.PromoCartItemRequest> items)
    {
        var menuItemIds = items.Select(i => i.MenuItemId).Distinct().ToList();
        var menuItems = await _orderRepository.GetMenuItemsByIdsAsync(menuItemIds);
        var menuItemsById = menuItems.ToDictionary(m => m.Id);

        var lines = new List<PromoDiscountCalculator.CartLine>();
        foreach (var item in items)
        {
            if (!menuItemsById.TryGetValue(item.MenuItemId, out var menuItem))
            {
                continue;
            }

            var availableAddOns = menuItem.MenuItemAddOns.ToDictionary(ma => ma.AddOnId, ma => ma.AddOn);
            var addOnsTotal = item.AddOnIds.Distinct()
                .Where(availableAddOns.ContainsKey)
                .Sum(id => availableAddOns[id].Price);

            var lineTotal = item.Quantity * (menuItem.Price + addOnsTotal);
            lines.Add(new PromoDiscountCalculator.CartLine(menuItem.Id, menuItem.Category, item.Quantity, lineTotal));
        }

        return (lines, lines.Sum(l => l.LineTotal));
    }
}
