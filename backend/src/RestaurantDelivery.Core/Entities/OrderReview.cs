namespace RestaurantDelivery.Core.Entities;

// One customer's review, either of one delivered order (at most one per Order - see
// OrderReviewConfiguration's unique index - submitted via the rating link sent by
// WhatsAppNotificationService.SendPostDeliveryPointsNotificationAsync) or a general
// store-wide review with no specific order (OrderId/Order null - submitted via the
// SendLoyaltyWalletUpdateAsync "/rate/store" link, which has no order context at all).
public class OrderReview
{
    public int Id { get; set; }

    public int? OrderId { get; set; }
    public Order? Order { get; set; }

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
