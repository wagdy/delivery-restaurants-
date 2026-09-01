namespace RestaurantDelivery.Core.Entities;

// One browser/device subscription for Web Push. A user (typically a Captain) can have
// several — one per device/browser they've granted notification permission on.
// Named WebPushSubscription (not PushSubscription) to avoid colliding with
// Lib.Net.Http.WebPush.PushSubscription, which the push-sending code also references.
public class WebPushSubscription
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public AppUser User { get; set; } = null!;

    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
