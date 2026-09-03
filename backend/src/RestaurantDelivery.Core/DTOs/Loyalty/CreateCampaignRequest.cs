using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Loyalty;

public class CreateCampaignRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    // Must match an existing Category name (the admin UI sources this from a dropdown,
    // not free text) - null means the campaign counts every order item.
    [MaxLength(200)]
    public string? CategoryName { get; set; }

    [Range(1, 100)]
    public int TargetPunches { get; set; } = 1;

    public DateTime? EndDate { get; set; }
}
