using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Loyalty;

public class EarnPointsRequest
{
    [Required]
    public string CustomerId { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "CheckAmount must be greater than 0.")]
    public decimal CheckAmount { get; set; }

    public string? CheckReference { get; set; }
}
