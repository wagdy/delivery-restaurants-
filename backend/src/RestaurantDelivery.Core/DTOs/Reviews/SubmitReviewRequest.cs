using System.ComponentModel.DataAnnotations;

namespace RestaurantDelivery.Core.DTOs.Reviews;

// Public - a guest's own order can be reviewed too (matches this app's "guest checkout is
// a first-class flow" convention elsewhere), so this isn't gated on being signed in.
// ReviewService.SubmitReviewAsync separately validates the order exists, is actually
// Delivered, and doesn't already have a review, so none of that is trusted here.
//
// OrderId is optional: present when the customer arrived via an order-specific rating
// link (SendPostDeliveryPointsNotificationAsync's "/rate/order/{id}"), absent for a
// general store-wide review (SendLoyaltyWalletUpdateAsync's "/rate/store", which has no
// order context at all). There is deliberately no CustomerId field here - accepting a
// client-supplied customer id would let anyone attribute a fabricated review to any real
// customer's account with no verification. Attribution instead comes only from the
// caller's own JWT when they happen to be signed in (see ReviewsController.Submit),
// exactly like every other public endpoint in this app that captures an optional
// customer id.
public class SubmitReviewRequest
{
    public int? OrderId { get; set; }

    [Range(1, 5, ErrorMessage = "Overall rating must be between 1 and 5.")]
    public int OverallRating { get; set; }

    public List<SubmitReviewAnswerRequest> Answers { get; set; } = new();
}

public class SubmitReviewAnswerRequest
{
    [Required]
    public int SurveyQuestionId { get; set; }

    [Required, MaxLength(2000)]
    public string AnswerValue { get; set; } = string.Empty;
}
