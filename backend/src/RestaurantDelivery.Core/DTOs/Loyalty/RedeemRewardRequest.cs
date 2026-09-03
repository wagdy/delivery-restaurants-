using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Loyalty;

public class RedeemRewardRequest
{
    [Required]
    public string CustomerId { get; set; } = string.Empty;

    [Required]
    public Guid CampaignId { get; set; }

    [MaxLength(200)]
    public string? CheckReference { get; set; }
}
