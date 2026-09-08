namespace RestaurantDelivery.Core.Entities;

// One customer's review of one delivered order - at most one per Order (see
// OrderReviewConfiguration's unique index), submitted via the rating link sent by
// WhatsAppNotificationService.SendPostDeliveryPointsNotificationAsync.
public class OrderReview
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    // Null for a guest order's review - reviewing doesn't require an account, matching
    // this app's "guest checkout is a first-class flow" convention elsewhere.
    public string? CustomerId { get; set; }
    public AppUser? Customer { get; set; }

    // 1-5. The one mandatory figure shown as visual stars in the admin table; every other
    // question's response lives in ReviewAnswer instead.
    public int OverallRating { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ReviewAnswer> Answers { get; set; } = new List<ReviewAnswer>();
}
