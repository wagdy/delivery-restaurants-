using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Push;

// Mirrors the shape of the browser's PushSubscription.toJSON() output exactly,
// so the frontend can POST it straight through with no reshaping.
public class PushSubscriptionRequest
{
    [Required, MaxLength(2048)]
    public string Endpoint { get; set; } = string.Empty;

    [Required]
    public PushSubscriptionKeysRequest Keys { get; set; } = new();
}

public class PushSubscriptionKeysRequest
{
    [Required]
    public string P256dh { get; set; } = string.Empty;

    [Required]
    public string Auth { get; set; } = string.Empty;
}
