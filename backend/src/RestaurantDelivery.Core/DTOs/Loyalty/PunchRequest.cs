using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Loyalty;

// A manual punch applied by staff via the Scanner tool.
public class PunchRequest
{
    [Required]
    public string CustomerId { get; set; } = string.Empty;

    [Required]
    public Guid CampaignId { get; set; }

    [MaxLength(200)]
    public string? CheckReference { get; set; }
}
