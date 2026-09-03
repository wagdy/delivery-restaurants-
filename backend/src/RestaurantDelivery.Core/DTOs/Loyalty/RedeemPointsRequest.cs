using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Loyalty;

public class RedeemPointsRequest
{
    [Required]
    public string CustomerId { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "PointsToRedeem must be greater than 0.")]
    public int PointsToRedeem { get; set; }

    public string? CheckReference { get; set; }
}
