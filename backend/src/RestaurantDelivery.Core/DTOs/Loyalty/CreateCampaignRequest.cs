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

    // Kept nullable (not `DateTime`) deliberately: [Required] only fires for a JSON body
    // when the property can actually represent "missing" - null. A non-nullable DateTime
    // silently defaults to 0001-01-01 (or throws a raw JSON error for an explicit `null`)
    // instead of producing the clean "The EndDate field is required." validation error below.
    [Required(ErrorMessage = "Expiration date is required.")]
    public DateTime? EndDate { get; set; }

    // When true, CampaignService fires a WhatsApp broadcast to every active customer
    // announcing this new punch-card campaign - see WhatsAppBroadcastBackgroundService.
    // Defaults to false - a broadcast is opt-in per creation, never implicit.
    public bool NotifyCustomersViaWhatsApp { get; set; }
}
