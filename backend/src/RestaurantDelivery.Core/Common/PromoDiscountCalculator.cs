using System.Text.Json;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Core.Common;

// Shared by CheckoutService.ValidatePromoAsync (the customer-facing live preview) and
// OrderService.CreateAsync (the authoritative recalculation at order-placement time) so
// the two can never drift apart - a discount a customer sees at checkout is guaranteed
// to be exactly what gets charged, since both call sites run the same code over the
// same server-resolved prices rather than trusting a client-computed number.
public static class PromoDiscountCalculator
{
    // A projection of one cart line, already resolved against the database by the
    // caller (server-trusted prices, never taken from the client) - Category is the
    // MenuItem's own free-text Category name (see MenuItem.Category), not an id.
    public record CartLine(int MenuItemId, string Category, int Quantity, decimal LineTotal);

    public record Result(decimal ItemDiscountAmount, decimal DeliveryDiscountAmount, List<int> AffectedMenuItemIds, string Description);

    // categoryNamesById only needs entries for the categories a SpecificCategory promo
    // actually targets - pass an empty dictionary for any other DiscountType.
    public static Result Calculate(PromoCode promo, List<CartLine> lines, decimal deliveryFee, Dictionary<int, string> categoryNamesById)
    {
        var percent = promo.DiscountValue / 100m;
        var targetIds = ParseTargetIds(promo.TargetIds);

        switch (promo.DiscountType)
        {
            case PromoDiscountType.Percentage:
                return TargetedLines(lines, _ => true, percent, $"{promo.DiscountValue}% off your order");

            case PromoDiscountType.SpecificCategory:
                var targetCategoryNames = new HashSet<string>(
                    targetIds.Where(categoryNamesById.ContainsKey).Select(id => categoryNamesById[id]),
                    StringComparer.OrdinalIgnoreCase);
                return TargetedLines(
                    lines, l => targetCategoryNames.Contains(l.Category), percent, $"{promo.DiscountValue}% off selected categories");

            case PromoDiscountType.SpecificItem:
                var targetItemIds = new HashSet<int>(targetIds);
                return TargetedLines(
                    lines, l => targetItemIds.Contains(l.MenuItemId), percent, $"{promo.DiscountValue}% off selected items");

            case PromoDiscountType.DeliveryDiscount:
                return new Result(0m, Math.Round(deliveryFee * percent, 2), new List<int>(), $"{promo.DiscountValue}% off delivery");

            default:
                return new Result(0m, 0m, new List<int>(), string.Empty);
        }
    }

    public static List<int> ParseTargetIds(string? targetIds)
    {
        if (string.IsNullOrWhiteSpace(targetIds))
        {
            return new List<int>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<int>>(targetIds) ?? new List<int>();
        }
        catch (JsonException)
        {
            return new List<int>();
        }
    }

    private static Result TargetedLines(List<CartLine> lines, Func<CartLine, bool> predicate, decimal percent, string description)
    {
        var affected = lines.Where(predicate).ToList();
        var discount = Math.Round(affected.Sum(l => l.LineTotal) * percent, 2);
        return new Result(discount, 0m, affected.Select(l => l.MenuItemId).Distinct().ToList(), description);
    }
}
