using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Loyalty;

public class UpdateLoyaltySettingsRequest
{
    [Range(0.0001, 1000, ErrorMessage = "Points per L.E. must be greater than 0.")]
    public decimal PointsPerCurrencyUnit { get; set; }

    [Range(0.01, 100000, ErrorMessage = "Redemption value must be greater than 0.")]
    public decimal RedemptionValuePer100Points { get; set; }
}
